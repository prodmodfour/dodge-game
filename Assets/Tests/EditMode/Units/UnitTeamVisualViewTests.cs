using NUnit.Framework;
using ReactionTactics.Grid;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Tests.EditMode.Units
{
    public sealed class UnitTeamVisualViewTests
    {
        [Test]
        public void ApplyTeamVisualsUsesPlayerMaterialsForPlayerUnits()
        {
            var fixture = CreateFixture(TeamId.Player);

            try
            {
                fixture.VisualView.ApplyTeamVisuals();

                Assert.That(fixture.BodyRenderer.sharedMaterial, Is.SameAs(fixture.PlayerBodyMaterial));
                Assert.That(fixture.MarkerRenderer.sharedMaterial, Is.SameAs(fixture.PlayerMarkerMaterial));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void ApplyTeamVisualsUsesEnemyMaterialsForEnemyUnits()
        {
            var fixture = CreateFixture(TeamId.Enemy);

            try
            {
                fixture.VisualView.ApplyTeamVisuals();

                Assert.That(fixture.BodyRenderer.sharedMaterial, Is.SameAs(fixture.EnemyBodyMaterial));
                Assert.That(fixture.MarkerRenderer.sharedMaterial, Is.SameAs(fixture.EnemyMarkerMaterial));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        private static Fixture CreateFixture(TeamId team)
        {
            var root = new GameObject("Unit Team Visual Test");
            var tacticalUnit = root.AddComponent<TacticalUnit>();
            var visualView = root.AddComponent<UnitTeamVisualView>();
            var stats = ScriptableObject.CreateInstance<UnitStatsDefinition>();
            stats.Configure(
                "Visual Test Unit",
                maxHP: 10,
                maxAP: 6,
                movementAnimationSpeed: UnitStatsDefinition.DefaultMovementAnimationSpeed,
                meleeRange: UnitStatsDefinition.MinimumMeleeRange,
                teamColorHint: Color.white);

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            var bodyRenderer = body.AddComponent<MeshRenderer>();

            var marker = new GameObject("TeamSelectionMarker");
            marker.transform.SetParent(root.transform, false);
            var markerRenderer = marker.AddComponent<MeshRenderer>();

            var playerBodyMaterial = CreateMaterial("Player Body Test Material");
            var enemyBodyMaterial = CreateMaterial("Enemy Body Test Material");
            var playerMarkerMaterial = CreateMaterial("Player Marker Test Material");
            var enemyMarkerMaterial = CreateMaterial("Enemy Marker Test Material");

            tacticalUnit.Initialize(new UnitId(1), team, stats, GridPosition.Zero);
            visualView.Configure(
                tacticalUnit,
                bodyRenderer,
                markerRenderer,
                playerBodyMaterial,
                enemyBodyMaterial,
                playerMarkerMaterial,
                enemyMarkerMaterial);

            return new Fixture(
                root,
                stats,
                visualView,
                bodyRenderer,
                markerRenderer,
                playerBodyMaterial,
                enemyBodyMaterial,
                playerMarkerMaterial,
                enemyMarkerMaterial);
        }

        private static Material CreateMaterial(string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("UI/Default");
            Assert.That(shader, Is.Not.Null, "At least one built-in shader must be available for material tests.");

            return new Material(shader)
            {
                name = name
            };
        }

        private sealed class Fixture
        {
            public Fixture(
                GameObject root,
                UnitStatsDefinition stats,
                UnitTeamVisualView visualView,
                Renderer bodyRenderer,
                Renderer markerRenderer,
                Material playerBodyMaterial,
                Material enemyBodyMaterial,
                Material playerMarkerMaterial,
                Material enemyMarkerMaterial)
            {
                Root = root;
                Stats = stats;
                VisualView = visualView;
                BodyRenderer = bodyRenderer;
                MarkerRenderer = markerRenderer;
                PlayerBodyMaterial = playerBodyMaterial;
                EnemyBodyMaterial = enemyBodyMaterial;
                PlayerMarkerMaterial = playerMarkerMaterial;
                EnemyMarkerMaterial = enemyMarkerMaterial;
            }

            public GameObject Root { get; }

            public UnitStatsDefinition Stats { get; }

            public UnitTeamVisualView VisualView { get; }

            public Renderer BodyRenderer { get; }

            public Renderer MarkerRenderer { get; }

            public Material PlayerBodyMaterial { get; }

            public Material EnemyBodyMaterial { get; }

            public Material PlayerMarkerMaterial { get; }

            public Material EnemyMarkerMaterial { get; }

            public void Destroy()
            {
                Object.DestroyImmediate(Root);
                Object.DestroyImmediate(Stats);
                Object.DestroyImmediate(PlayerBodyMaterial);
                Object.DestroyImmediate(EnemyBodyMaterial);
                Object.DestroyImmediate(PlayerMarkerMaterial);
                Object.DestroyImmediate(EnemyMarkerMaterial);
            }
        }
    }
}
