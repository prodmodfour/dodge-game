using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;

namespace ReactionTactics.Tests.EditMode
{
    public sealed class ReactionTacticsSmokeTests
    {
        private const string RuntimeAssemblyName = "ReactionTactics.Runtime";
        private const string RuntimeAssemblyDefinitionPath = "Assets/Scripts/ReactionTactics/Runtime/ReactionTactics.Runtime.asmdef";

        [Test]
        public void RuntimeAssemblyDefinitionLoads()
        {
            var assemblyDefinitionPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(RuntimeAssemblyName);

            Assert.That(assemblyDefinitionPath, Is.EqualTo(RuntimeAssemblyDefinitionPath));

            var assemblyDefinition = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assemblyDefinitionPath);
            Assert.That(
                assemblyDefinition,
                Is.Not.Null,
                $"Expected Unity to load the {RuntimeAssemblyName} assembly definition from {RuntimeAssemblyDefinitionPath}.");
            Assert.That(assemblyDefinition.name, Is.EqualTo(RuntimeAssemblyName));
        }
    }
}
