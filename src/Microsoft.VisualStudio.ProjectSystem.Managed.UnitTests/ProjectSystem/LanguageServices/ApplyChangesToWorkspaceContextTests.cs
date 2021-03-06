﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.LanguageServices.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Logging;

using Moq;

using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.LanguageServices
{
    public class ApplyChangesToWorkspaceContextTests
    {
        [Fact]
        public async Task DisposeAsync_WhenNotInitialized_DoesNotThrow()
        {
            var applyChangesToWorkspace = CreateInstance();

            await applyChangesToWorkspace.DisposeAsync();

            Assert.True(applyChangesToWorkspace.IsDisposed);
        }

        [Fact]
        public async Task DisposeAsync_WhenInitializedWithNoHandlers_DoesNotThrow()
        {
            var applyChangesToWorkspace = CreateInstance();
            var context = IWorkspaceProjectContextMockFactory.Create();
            applyChangesToWorkspace.Initialize(context);

            await applyChangesToWorkspace.DisposeAsync();

            Assert.True(applyChangesToWorkspace.IsDisposed);
        }

        [Fact]
        public async Task DisposeAsync_DisposesHandlers()
        {
            int callCount = 0;
            var handler = IWorkspaceContextHandlerFactory.ImplementDispose(() => { callCount++; });

            var applyChangesToWorkspace = CreateInstance(handlers: handler);
            var context = IWorkspaceProjectContextMockFactory.Create();

            applyChangesToWorkspace.Initialize(context);

            await applyChangesToWorkspace.DisposeAsync();

            Assert.Equal(1, callCount);
        }

        [Fact]
        public void Initialize_NullAsContext_ThrowsArgumentNull()
        {
            var applyChangesToWorkspace = CreateInstance();

            Assert.Throws<ArgumentNullException>("context", () =>
            {
                applyChangesToWorkspace.Initialize(null);
            });
        }

        [Fact]
        public void Initialize_WhenAlreadyInitialized_ThrowsInvalidOperation()
        {
            var applyChangesToWorkspace = CreateInstance();
            var context = IWorkspaceProjectContextMockFactory.Create();

            applyChangesToWorkspace.Initialize(context);

            Assert.Throws<InvalidOperationException>(() =>
            {
                applyChangesToWorkspace.Initialize(context);
            });
        }

        [Fact]
        public void Initialize_InitializesHandlers()
        {
            IWorkspaceProjectContext result = null;
            var handler = IWorkspaceContextHandlerFactory.ImplementInitialize((c) => { result = c; });

            var applyChangesToWorkspace = CreateInstance(handlers: handler);
            var context = IWorkspaceProjectContextMockFactory.Create();

            applyChangesToWorkspace.Initialize(context);

            Assert.Same(context, result);
        }

        [Fact]
        public void GetProjectEvaluationRules_WhenNotInitialized_ThrowsInvalidOperation()
        {
            var applyChangesToWorkspace = CreateInstance();

            Assert.Throws<InvalidOperationException>(() =>
            {
                applyChangesToWorkspace.GetProjectEvaluationRules();
            });
        }

        [Fact]
        public void GetDesignTimeRules_WhenNotInitialized_ThrowsInvalidOperation()
        {
            var applyChangesToWorkspace = CreateInstance();

            Assert.Throws<InvalidOperationException>(() =>
            {
                applyChangesToWorkspace.GetProjectBuildRules();
            });
        }

        [Fact]
        public void ApplyProjectEvaluation_WhenNotInitialized_ThrowsInvalidOperation()
        {
            var applyChangesToWorkspace = CreateInstance();

            var update = Mock.Of<IProjectVersionedValue<IProjectSubscriptionUpdate>>();

            Assert.Throws<InvalidOperationException>(() =>
            {
                applyChangesToWorkspace.ApplyProjectEvaluation(update, false, CancellationToken.None);
            });
        }

        [Fact]
        public void ApplyProjectBuild_WhenNotInitialized_ThrowsInvalidOperation()
        {
            var applyChangesToWorkspace = CreateInstance();

            var update = Mock.Of<IProjectVersionedValue<IProjectSubscriptionUpdate>>();

            Assert.Throws<InvalidOperationException>(() =>
            {
                applyChangesToWorkspace.ApplyProjectBuild(update, false, CancellationToken.None);
            });
        }

        [Fact]
        public async Task Initialize_WhenDisposed_ThrowsObjectDisposed()
        {
            var applyChangesToWorkspace = CreateInstance();

            await applyChangesToWorkspace.DisposeAsync();

            var context = IWorkspaceProjectContextMockFactory.Create();
            Assert.Throws<ObjectDisposedException>(() =>
            {
                applyChangesToWorkspace.Initialize(context);
            });
        }

        [Fact]
        public async Task GetEvaluationRules_WhenDisposed_ThrowsObjectDisposed()
        {
            var applyChangesToWorkspace = CreateInstance();

            await applyChangesToWorkspace.DisposeAsync();

            var context = IWorkspaceProjectContextMockFactory.Create();
            Assert.Throws<ObjectDisposedException>(() =>
            {
                applyChangesToWorkspace.GetProjectEvaluationRules();
            });
        }

        [Fact]
        public async Task GetDesignTimeRules_WhenDisposed_ThrowsObjectDisposed()
        {
            var applyChangesToWorkspace = CreateInstance();

            await applyChangesToWorkspace.DisposeAsync();

            var context = IWorkspaceProjectContextMockFactory.Create();
            Assert.Throws<ObjectDisposedException>(() =>
            {
                applyChangesToWorkspace.GetProjectBuildRules();
            });
        }

        [Fact]
        public async Task ApplyProjectEvaluation_WhenDisposed_ThrowsObjectDisposed()
        {
            var applyChangesToWorkspace = CreateInstance();

            await applyChangesToWorkspace.DisposeAsync();

            var update = Mock.Of<IProjectVersionedValue<IProjectSubscriptionUpdate>>();

            Assert.Throws<ObjectDisposedException>(() =>
            {
                applyChangesToWorkspace.ApplyProjectEvaluation(update, true, CancellationToken.None);
            });
        }

        [Fact]
        public async Task ApplyProjectBuild_WhenDisposed_ThrowsObjectDisposed()
        {
            var applyChangesToWorkspace = CreateInstance();

            await applyChangesToWorkspace.DisposeAsync();

            var update = Mock.Of<IProjectVersionedValue<IProjectSubscriptionUpdate>>();

            Assert.Throws<ObjectDisposedException>(() =>
            {
                applyChangesToWorkspace.ApplyProjectBuild(update, true, CancellationToken.None);
            });
        }

        [Fact]
        public void GetProjectEvaluationRules_ReturnsAllProjectEvaluationRuleNames()
        {
            var handler1 = IEvaluationHandlerFactory.ImplementProjectEvaluationRule("Rule1");
            var handler2 = IEvaluationHandlerFactory.ImplementProjectEvaluationRule("Rule2");

            var applyChangesToWorkspace = CreateInitializedInstance(handlers: new[] { handler1, handler2 });

            var result = applyChangesToWorkspace.GetProjectEvaluationRules();

            Assert.Equal(new[] { "Rule1", "Rule2" }, result.OrderBy(n => n));
        }

        [Fact]
        public void GetDesignTimeRules_ReturnsCompilerCommandLineArgs()
        {
            var applyChangesToWorkspace = CreateInitializedInstance();

            var result = applyChangesToWorkspace.GetProjectBuildRules();

            Assert.Single(result, "CompilerCommandLineArgs");
        }

        [Fact]
        public void ApplyProjectEvaluation_WhenNoRuleChanges_DoesNotCallHandler()
        {
            int callCount = 0;
            var handler = IEvaluationHandlerFactory.ImplementHandle("RuleName", (version, description, isActiveContext, logger) => { callCount++; });

            var applyChangesToWorkspace = CreateInitializedInstance(handlers: new[] { handler });

            var update = IProjectVersionedValueFactory.FromJson(
@"{
   ""ProjectChanges"": {
        ""RuleName"": {
            ""Difference"": { 
                ""AnyChanges"": false
            },
        }
    }
}");

            applyChangesToWorkspace.ApplyProjectEvaluation(update, isActiveContext: false, CancellationToken.None);

            Assert.Equal(0, callCount);
        }

        [Fact]
        public void ApplyProjectBuild_WhenNoCompilerCommandLineArgsRuleChanges_DoesNotCallHandler()
        {
            int callCount = 0;
            var handler = ICommandLineHandlerFactory.ImplementHandle((version, added, removed, isActiveContext, logger) => { callCount++; });

            var applyChangesToWorkspace = CreateInitializedInstance(handlers: new[] { handler });

            var update = IProjectVersionedValueFactory.FromJson(
@"{
   ""ProjectChanges"": {
        ""CompilerCommandLineArgs"": {
            ""Difference"": { 
                ""AnyChanges"": false
            },
        }
    }
}");

            applyChangesToWorkspace.ApplyProjectBuild(update, isActiveContext: false, CancellationToken.None);

            Assert.Equal(0, callCount);
        }

        [Fact]
        public void ApplyProjectEvaluation_CallsHandler()
        {
            (IComparable version, IProjectChangeDescription description, bool isActiveContext, IProjectLogger logger) result = default;

            var handler = IEvaluationHandlerFactory.ImplementHandle("RuleName", (version, description, isActiveContext, logger) =>
            {
                result = (version, description, isActiveContext, logger);
            });

            var applyChangesToWorkspace = CreateInitializedInstance(handlers: new[] { handler });

            var update = IProjectVersionedValueFactory.FromJson(version: 2,
@"{
   ""ProjectChanges"": {
        ""RuleName"": {
            ""Difference"": { 
                ""AnyChanges"": true
            },
        }
    }
}");
            applyChangesToWorkspace.ApplyProjectEvaluation(update, isActiveContext: true, CancellationToken.None);

            Assert.Equal(2, result.version);
            Assert.NotNull(result.description);
            Assert.True(result.isActiveContext);
            Assert.NotNull(result.logger);
        }

        [Fact]
        public void ApplyProjectBuild_ParseCommandLineAndCallsHandler()
        {
            (IComparable version, BuildOptions added, BuildOptions removed, bool isActiveContext, IProjectLogger logger) result = default;

            var handler = ICommandLineHandlerFactory.ImplementHandle((version, added, removed, isActiveContext, logger) =>
            {
                result = (version, added, removed, isActiveContext, logger);
            });

            var parser = ICommandLineParserServiceFactory.CreateCSharp();

            var applyChangesToWorkspace = CreateInitializedInstance(commandLineParser: parser, handlers: new[] { handler });

            var update = IProjectVersionedValueFactory.FromJson(version: 2,
@"{
   ""ProjectChanges"": {
        ""CompilerCommandLineArgs"": {
            ""Difference"": { 
                ""AnyChanges"": true,
                ""AddedItems"" : [ ""/reference:Added.dll"" ],
                ""RemovedItems"" : [ ""/reference:Removed.dll"" ]
            }
        }
    }
}");
            applyChangesToWorkspace.ApplyProjectBuild(update, isActiveContext: true, CancellationToken.None);

            Assert.Equal(2, result.version);
            Assert.True(result.isActiveContext);
            Assert.NotNull(result.logger);

            Assert.Single(result.added.MetadataReferences.Select(r => r.Reference), "Added.dll");
            Assert.Single(result.removed.MetadataReferences.Select(r => r.Reference), "Removed.dll");
        }

        [Fact]
        public void ApplyProjectEvaluation_WhenCancellationTokenCancelled_StopsProcessingHandlers()
        {
            var cancellationTokenSource = new CancellationTokenSource();

            var handler1 = IEvaluationHandlerFactory.ImplementHandle("RuleName1", (version, description, isActiveContext, logger) =>
            {
                cancellationTokenSource.Cancel();
            });

            int callCount = 0;
            var handler2 = IEvaluationHandlerFactory.ImplementHandle("RuleName2", (version, description, isActiveContext, logger) =>
            {
                callCount++;
            });

            var applyChangesToWorkspace = CreateInitializedInstance(handlers: new[] { handler1, handler2 });

            var update = IProjectVersionedValueFactory.FromJson(
@"{
   ""ProjectChanges"": {
        ""RuleName1"": {
            ""Difference"": { 
                ""AnyChanges"": true
            },
        },
        ""RuleName2"": {
            ""Difference"": { 
                ""AnyChanges"": true
            },
        }
    }
}");
            applyChangesToWorkspace.ApplyProjectEvaluation(update, isActiveContext: true, cancellationTokenSource.Token);

            Assert.True(cancellationTokenSource.IsCancellationRequested);
            Assert.Equal(0, callCount);
        }

        [Fact]
        public void ApplyProjectBuild_WhenCancellationTokenCancelled_StopsProcessingHandlers()
        {
            var cancellationTokenSource = new CancellationTokenSource();

            var handler1 = ICommandLineHandlerFactory.ImplementHandle((version, added, removed, isActiveContext, logger) =>
            {
                cancellationTokenSource.Cancel();
            });

            int callCount = 0;
            var handler2 = ICommandLineHandlerFactory.ImplementHandle((version, added, removed, isActiveContext, logger) =>
            {
                callCount++;
            });

            var applyChangesToWorkspace = CreateInitializedInstance(handlers: new[] { handler1, handler2 });

            var update = IProjectVersionedValueFactory.FromJson(
@"{
   ""ProjectChanges"": {
        ""CompilerCommandLineArgs"": {
            ""Difference"": { 
                ""AnyChanges"": true
            },
        }
    }
}");
            applyChangesToWorkspace.ApplyProjectBuild(update, isActiveContext: true, cancellationTokenSource.Token);

            Assert.True(cancellationTokenSource.IsCancellationRequested);
            Assert.Equal(0, callCount);
        }


        [Fact]
        public void ApplyProjectEvaluation_IgnoresCommandLineHandlers()
        {
            int callCount = 0;
            var handler = ICommandLineHandlerFactory.ImplementHandle((version, added, removed, isActiveContext, logger) => { callCount++; });

            var applyChangesToWorkspace = CreateInitializedInstance(handlers: new[] { handler });

            var update = IProjectVersionedValueFactory.FromJson(
@"{
   ""ProjectChanges"": {
        ""CompilerCommandLineArgs"": {
            ""Difference"": { 
                ""AnyChanges"": true
            },
        },
        ""RuleName"": {
            ""Difference"": { 
                ""AnyChanges"": true
            },
        }
    }
}");
            applyChangesToWorkspace.ApplyProjectEvaluation(update, isActiveContext: true, CancellationToken.None);

            Assert.Equal(0, callCount);
        }


        [Fact]
        public void ApplyProjectBuild_IgnoresEvaluationHandlers()
        {
            int callCount = 0;
            var handler = IEvaluationHandlerFactory.ImplementHandle("RuleName", (version, description, isActiveContext, logger) => { callCount++; });

            var applyChangesToWorkspace = CreateInitializedInstance(handlers: new[] { handler });

            var update = IProjectVersionedValueFactory.FromJson(
@"{
   ""ProjectChanges"": {
        ""CompilerCommandLineArgs"": {
            ""Difference"": { 
                ""AnyChanges"": true
            },
        },
        ""RuleName"": {
            ""Difference"": { 
                ""AnyChanges"": true
            },
        }
    }
}");
            applyChangesToWorkspace.ApplyProjectBuild(update, isActiveContext: true, CancellationToken.None);

            Assert.Equal(0, callCount);
        }


        [Fact]
        public void ApplyProjectBuild_WhenDesignTimeBuildFails_SetsLastDesignTimeBuildSucceededToFalse()
        {
            var applyChangesToWorkspace = CreateInitializedInstance(out var context);
            context.LastDesignTimeBuildSucceeded = true;

            var update = IProjectVersionedValueFactory.FromJson(
@"{
   ""ProjectChanges"": {
        ""CompilerCommandLineArgs"": {
            ""Difference"": { 
                ""AnyChanges"": true
            },
            ""After"": { 
                ""EvaluationSucceeded"": false
            }
        }
    }
}");

            applyChangesToWorkspace.ApplyProjectBuild(update, isActiveContext: false, CancellationToken.None);

            Assert.False(context.LastDesignTimeBuildSucceeded);
        }

        [Fact]
        public void ApplyProjectBuild_AfterFailingDesignTimeBuildSucceeds_SetsLastDesignTimeBuildSucceededToTrue()
        {
            var applyChangesToWorkspace = CreateInitializedInstance(out var context);

            var update = IProjectVersionedValueFactory.FromJson(
@"{
   ""ProjectChanges"": {
        ""CompilerCommandLineArgs"": {
            ""Difference"": { 
                ""AnyChanges"": true
            },
            ""After"": { 
                ""EvaluationSucceeded"": true
            }
        }
    }
}");

            applyChangesToWorkspace.ApplyProjectBuild(update, isActiveContext: false, CancellationToken.None);

            Assert.True(context.LastDesignTimeBuildSucceeded);
        }

        private static ApplyChangesToWorkspaceContext CreateInitializedInstance(ConfiguredProject project = null, ICommandLineParserService commandLineParser = null, IProjectLogger logger = null, params IWorkspaceContextHandler[] handlers)
        {
            return CreateInitializedInstance(out _, project, commandLineParser, logger, handlers);
        }

        private static ApplyChangesToWorkspaceContext CreateInitializedInstance(out IWorkspaceProjectContext context, ConfiguredProject project = null, ICommandLineParserService commandLineParser = null, IProjectLogger logger = null, params IWorkspaceContextHandler[] handlers)
        {
            var applyChangesToWorkspace = CreateInstance(project, commandLineParser, logger, handlers);
            context = IWorkspaceProjectContextMockFactory.Create();

            applyChangesToWorkspace.Initialize(context);

            return applyChangesToWorkspace;
        }

        private static ApplyChangesToWorkspaceContext CreateInstance(ConfiguredProject project = null, ICommandLineParserService commandLineParser = null, IProjectLogger logger = null, params IWorkspaceContextHandler[] handlers)
        {
            if (project == null)
            {
                var unconfiguredProject = UnconfiguredProjectFactory.ImplementFullPath(@"C:\Project\Project.csproj");

                project = ConfiguredProjectFactory.ImplementUnconfiguredProject(unconfiguredProject);
            }

            commandLineParser = commandLineParser ?? ICommandLineParserServiceFactory.Create();
            logger = logger ?? IProjectLoggerFactory.Create();

            var factories = handlers.Select(h => ExportFactoryFactory.ImplementCreateValueWithAutoDispose(() => h))
                                    .ToArray();

            var applyChangesToWorkspaceContext = new ApplyChangesToWorkspaceContext(project, logger, factories);

            applyChangesToWorkspaceContext.CommandLineParsers.Add(commandLineParser);

            return applyChangesToWorkspaceContext;
        }
    }
}
