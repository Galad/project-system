﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.ProjectSystem.VS.Tree.Dependencies.Models;
using Microsoft.VisualStudio.ProjectSystem.VS.Tree.Dependencies.Snapshot;
using Microsoft.VisualStudio.ProjectSystem.VS.Tree.Dependencies.Subscriptions;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Tree.Dependencies.CrossTarget
{
    [Export(typeof(IDependencyCrossTargetSubscriber))]
    [AppliesTo(ProjectCapability.DependenciesTree)]
    internal class DependencySharedProjectsSubscriber : OnceInitializedOnceDisposed, IDependencyCrossTargetSubscriber
    {
#pragma warning disable CA2213 // OnceInitializedOnceDisposedAsync are not tracked correctly by the IDisposeable analyzer
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(initialCount: 1);
#pragma warning restore CA2213
        private readonly List<IDisposable> _subscriptionLinks = new List<IDisposable>();
        private readonly IProjectAsynchronousTasksService _tasksService;
        private readonly IDependenciesSnapshotProvider _dependenciesSnapshotProvider;
        private ICrossTargetSubscriptionsHost _host;

        [ImportingConstructor]
        public DependencySharedProjectsSubscriber(
            [Import(ExportContractNames.Scopes.UnconfiguredProject)] IProjectAsynchronousTasksService tasksService,
            IDependenciesSnapshotProvider dependenciesSnapshotProvider)
            : base(synchronousDisposal: true)
        {
            _tasksService = tasksService;
            _dependenciesSnapshotProvider = dependenciesSnapshotProvider;
        }

        public void InitializeSubscriber(ICrossTargetSubscriptionsHost host, IProjectSubscriptionService subscriptionService)
        {
            _host = host;

            SubscribeToConfiguredProject(subscriptionService);
        }

        public void AddSubscriptions(AggregateCrossTargetProjectContext newProjectContext)
        {
            Requires.NotNull(newProjectContext, nameof(newProjectContext));

            foreach (ConfiguredProject configuredProject in newProjectContext.InnerConfiguredProjects)
            {
                SubscribeToConfiguredProject(configuredProject.Services.ProjectSubscription);
            }
        }

        public void ReleaseSubscriptions()
        {
            foreach (IDisposable link in _subscriptionLinks)
            {
                link.Dispose();
            }

            _subscriptionLinks.Clear();
        }

        private void SubscribeToConfiguredProject(IProjectSubscriptionService subscriptionService)
        {
            // Use an intermediate buffer block for project rule data to allow subsequent blocks
            // to only observe specific rule name(s).

            var intermediateBlock =
                new BufferBlock<IProjectVersionedValue<IProjectSubscriptionUpdate>>(
                    new ExecutionDataflowBlockOptions()
                    {
                        NameFormat = "Dependencies Shared Projects Input: {1}"
                    });

            _subscriptionLinks.Add(
                subscriptionService.ProjectRuleSource.SourceBlock.LinkTo(
                    intermediateBlock,
                    ruleNames: ConfigurationGeneral.SchemaName,
                    suppressVersionOnlyUpdates: false,
                    linkOptions: DataflowOption.PropagateCompletion));

            var actionBlock =
                DataflowBlockSlim.CreateActionBlock<IProjectVersionedValue<Tuple<IProjectSubscriptionUpdate, IProjectSharedFoldersSnapshot, IProjectCatalogSnapshot>>>(
                    e => OnProjectChangedAsync(e.Value),
                    new ExecutionDataflowBlockOptions()
                    {
                        NameFormat = "Dependencies Shared Projects Input: {1}"
                    });

            _subscriptionLinks.Add(ProjectDataSources.SyncLinkTo(
                intermediateBlock.SyncLinkOptions(),
                subscriptionService.SharedFoldersSource.SourceBlock.SyncLinkOptions(),
                subscriptionService.ProjectCatalogSource.SourceBlock.SyncLinkOptions(),
                actionBlock,
                linkOptions: DataflowOption.PropagateCompletion));
        }

        private async Task OnProjectChangedAsync(Tuple<IProjectSubscriptionUpdate, IProjectSharedFoldersSnapshot, IProjectCatalogSnapshot> e)
        {
            if (IsDisposing || IsDisposed)
            {
                return;
            }

            EnsureInitialized();

            await _tasksService.LoadedProjectAsync(() =>
            {
                _tasksService.UnloadCancellationToken.ThrowIfCancellationRequested();

                return HandleAsync(e);
            });
        }

        private async Task HandleAsync(Tuple<IProjectSubscriptionUpdate, IProjectSharedFoldersSnapshot, IProjectCatalogSnapshot> e)
        {
            AggregateCrossTargetProjectContext currentAggregateContext = await _host.GetCurrentAggregateProjectContext();
            if (currentAggregateContext == null)
            {
                return;
            }

            IProjectSubscriptionUpdate projectUpdate = e.Item1;
            IProjectSharedFoldersSnapshot sharedProjectsUpdate = e.Item2;
            IProjectCatalogSnapshot catalogs = e.Item3;

            // We need to process the update within a lock to ensure that we do not release this context during processing.
            // TODO: Enable concurrent execution of updates themselves, i.e. two separate invocations of HandleAsync
            //       should be able to run concurrently.
            using (await _gate.DisposableWaitAsync())
            {
                // Get the inner workspace project context to update for this change.
                ITargetedProjectContext projectContextToUpdate = currentAggregateContext
                    .GetInnerProjectContext(projectUpdate.ProjectConfiguration, out bool isActiveContext);

                if (projectContextToUpdate == null)
                {
                    return;
                }

                var dependencyChangeContext = new DependenciesRuleChangeContext(
                    currentAggregateContext.ActiveProjectContext.TargetFramework, 
                    catalogs);

                ProcessSharedProjectsUpdates(sharedProjectsUpdate, projectContextToUpdate, dependencyChangeContext);

                if (dependencyChangeContext.AnyChanges)
                {
                    DependenciesChanged?.Invoke(this, new DependencySubscriptionChangedEventArgs(dependencyChangeContext));
                }
            }
        }

        private void ProcessSharedProjectsUpdates(
            IProjectSharedFoldersSnapshot sharedFolders,
            ITargetedProjectContext targetContext,
            DependenciesRuleChangeContext dependencyChangeContext)
        {
            Requires.NotNull(sharedFolders, nameof(sharedFolders));
            Requires.NotNull(targetContext, nameof(targetContext));
            Requires.NotNull(dependencyChangeContext, nameof(dependencyChangeContext));

            IDependenciesSnapshot snapshot = _dependenciesSnapshotProvider.CurrentSnapshot;
            if (!snapshot.Targets.TryGetValue(targetContext.TargetFramework, out ITargetedDependenciesSnapshot targetedSnapshot))
            {
                return;
            }

            IEnumerable<string> sharedFolderProjectPaths = sharedFolders.Value.Select(sf => sf.ProjectPath);
            var currentSharedImportNodes = targetedSnapshot.TopLevelDependencies
                .Where(x => x.Flags.Contains(DependencyTreeFlags.SharedProjectFlags))
                .ToList();
            IEnumerable<string> currentSharedImportNodePaths = currentSharedImportNodes.Select(x => x.Path);

            // process added nodes
            IEnumerable<string> addedSharedImportPaths = sharedFolderProjectPaths.Except(currentSharedImportNodePaths);
            foreach (string addedSharedImportPath in addedSharedImportPaths)
            {
                IDependencyModel added = new SharedProjectDependencyModel(
                    addedSharedImportPath,
                    addedSharedImportPath,
                    isResolved: true,
                    isImplicit: false,
                    properties: ImmutableStringDictionary<string>.EmptyOrdinal);
                dependencyChangeContext.IncludeAddedChange(targetContext.TargetFramework, added);
            }

            // process removed nodes
            IEnumerable<string> removedSharedImportPaths = currentSharedImportNodePaths.Except(sharedFolderProjectPaths);
            foreach (string removedSharedImportPath in removedSharedImportPaths)
            {
                bool exists = currentSharedImportNodes.Any(node => PathHelper.IsSamePath(node.Path, removedSharedImportPath));

                if (exists)
                {
                    dependencyChangeContext.IncludeRemovedChange(
                        targetContext.TargetFramework, 
                        ProjectRuleHandler.ProviderTypeString, 
                        dependencyId: removedSharedImportPath);
                }
            }
        }

        public event EventHandler<DependencySubscriptionChangedEventArgs> DependenciesChanged;

        protected override void Initialize()
        {   
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ReleaseSubscriptions();
            }
        }
    }
}
