using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Units;
using UnityCliConnector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ReactionTactics.Editor
{
    [UnityCliTool(Name = "rt_polish_prototype_visuals", Description = "Apply the prototype readability pass for team colors, tile seams, lighting, camera, and unit markers.", Group = "reaction-tactics")]
    public static class PrototypeVisualsPolishTool
    {
        private const string DefaultScenePath = "Assets/Scenes/MainPrototype.unity";
        private const string MaterialsFolderPath = "Assets/Materials";
        private const string PrototypeUnitPrefabPath = "Assets/Prefabs/PrototypeUnit.prefab";
        private const string BodyChildName = "Body";
        private const string TeamMarkerChildName = "TeamSelectionMarker";
        private const string ActiveMarkerChildName = "ActiveTurnMarker";
        private const string ReactionMarkerChildName = "ReactionTurnMarker";
        private const float TileHorizontalScaleFactor = 0.92f;

        private static readonly MaterialSpec[] MaterialSpecs =
        {
            new MaterialSpec("Assets/Materials/PrototypeGridWalkable.mat", "Prototype Grid Walkable", new Color(0.30f, 0.56f, 0.34f, 1f), 0.30f, Color.black),
            new MaterialSpec("Assets/Materials/PrototypeGridBlocked.mat", "Prototype Grid Blocked", new Color(0.22f, 0.18f, 0.16f, 1f), 0.25f, Color.black),
            new MaterialSpec("Assets/Materials/PrototypeGridLineOfSightBlocker.mat", "Prototype Grid Line Of Sight Blocker", new Color(0.42f, 0.25f, 0.68f, 1f), 0.35f, new Color(0.08f, 0.04f, 0.15f, 1f)),
            new MaterialSpec("Assets/Materials/PrototypeGridHighlight.mat", "Prototype Grid Highlight", new Color(1.00f, 0.86f, 0.12f, 1f), 0.18f, new Color(0.35f, 0.25f, 0.02f, 1f)),
            new MaterialSpec("Assets/Materials/PrototypeGridDanger.mat", "Prototype Grid Danger", new Color(1.00f, 0.22f, 0.08f, 1f), 0.20f, new Color(0.35f, 0.05f, 0.02f, 1f)),
            new MaterialSpec("Assets/Materials/PrototypeGridSafe.mat", "Prototype Grid Safe", new Color(0.06f, 0.78f, 0.92f, 1f), 0.20f, new Color(0.01f, 0.22f, 0.27f, 1f)),
            new MaterialSpec("Assets/Materials/PrototypeUnitBody.mat", "Prototype Unit Body", new Color(0.70f, 0.76f, 0.82f, 1f), 0.34f, Color.black),
            new MaterialSpec("Assets/Materials/PrototypeUnitMarker.mat", "Prototype Unit Marker", new Color(0.18f, 0.55f, 1f, 1f), 0.18f, new Color(0.02f, 0.12f, 0.25f, 1f)),
            new MaterialSpec("Assets/Materials/PrototypeTeamPlayer.mat", "Prototype Team Player", new Color(0.10f, 0.34f, 1.00f, 1f), 0.28f, new Color(0.01f, 0.08f, 0.32f, 1f)),
            new MaterialSpec("Assets/Materials/PrototypeTeamEnemy.mat", "Prototype Team Enemy", new Color(0.95f, 0.13f, 0.08f, 1f), 0.28f, new Color(0.32f, 0.02f, 0.01f, 1f)),
            new MaterialSpec("Assets/Materials/PrototypeTeamPlayerMarker.mat", "Prototype Team Player Marker", new Color(0.18f, 0.74f, 1.00f, 1f), 0.12f, new Color(0.02f, 0.20f, 0.35f, 1f)),
            new MaterialSpec("Assets/Materials/PrototypeTeamEnemyMarker.mat", "Prototype Team Enemy Marker", new Color(1.00f, 0.36f, 0.10f, 1f), 0.12f, new Color(0.35f, 0.08f, 0.02f, 1f)),
            new MaterialSpec("Assets/Materials/PrototypeActiveMarker.mat", "Prototype Active Marker", new Color(1.00f, 0.88f, 0.05f, 1f), 0.10f, new Color(0.45f, 0.32f, 0.02f, 1f)),
            new MaterialSpec("Assets/Materials/PrototypeReactionMarker.mat", "Prototype Reaction Marker", new Color(0.05f, 0.95f, 1.00f, 1f), 0.10f, new Color(0.01f, 0.32f, 0.38f, 1f))
        };

        public sealed class Parameters
        {
            [ToolParameter("Scene asset path to polish.", DefaultValue = DefaultScenePath)]
            public string ScenePath { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            try
            {
                var toolParams = new ToolParams(parameters ?? new JObject());
                var scenePath = toolParams.Get("scenePath", toolParams.Get("scene_path", DefaultScenePath)).Trim();
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    return new ErrorResponse("Parameter 'scenePath' cannot be empty.");
                }

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    return new ErrorResponse($"Scene asset '{scenePath}' was not found.");
                }

                var materials = EnsureMaterials();
                var prefabSummary = EnsurePrototypeUnitPrefabVisuals(materials);
                var sceneSummary = ApplySceneVisuals(scenePath);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return new SuccessResponse("Applied prototype visual readability polish.", new
                {
                    scene = scenePath,
                    tileHorizontalScaleFactor = TileHorizontalScaleFactor,
                    materials = materials.ToSummary(),
                    prefab = prefabSummary,
                    sceneVisuals = sceneSummary
                });
            }
            catch (Exception exception)
            {
                return new ErrorResponse($"Failed to polish prototype visuals: {exception.Message}");
            }
        }

        private static MaterialSet EnsureMaterials()
        {
            EnsureMaterialsFolderExists();
            var createdPaths = new List<string>();
            var updatedPaths = new List<string>();
            var materialsByPath = new Dictionary<string, Material>(StringComparer.Ordinal);

            foreach (var spec in MaterialSpecs)
            {
                var existed = AssetDatabase.LoadAssetAtPath<Material>(spec.Path) != null;
                var material = EnsureMaterial(spec);
                materialsByPath[spec.Path] = material;
                if (existed)
                {
                    updatedPaths.Add(spec.Path);
                }
                else
                {
                    createdPaths.Add(spec.Path);
                }
            }

            AssetDatabase.SaveAssets();
            foreach (var spec in MaterialSpecs)
            {
                AssetDatabase.ImportAsset(spec.Path, ImportAssetOptions.ForceUpdate);
            }

            return new MaterialSet(materialsByPath, createdPaths, updatedPaths);
        }

        private static void EnsureMaterialsFolderExists()
        {
            if (!AssetDatabase.IsValidFolder(MaterialsFolderPath))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }
        }

        private static Material EnsureMaterial(MaterialSpec spec)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(spec.Path);
            if (material == null)
            {
                material = new Material(FindReadableShader())
                {
                    name = spec.Name
                };
                ApplyMaterialSpec(material, spec);
                AssetDatabase.CreateAsset(material, spec.Path);
            }
            else
            {
                material.name = spec.Name;
                if (material.shader == null || string.Equals(material.shader.name, "Hidden/InternalErrorShader", StringComparison.Ordinal))
                {
                    material.shader = FindReadableShader();
                }

                ApplyMaterialSpec(material, spec);
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static Shader FindReadableShader()
        {
            var shader = Shader.Find("Standard")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Hidden/InternalErrorShader");
            if (shader == null)
            {
                throw new InvalidOperationException("No supported material shader was found for prototype visuals.");
            }

            return shader;
        }

        private static void ApplyMaterialSpec(Material material, MaterialSpec spec)
        {
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", spec.BaseColor);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", spec.BaseColor);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", spec.Smoothness);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", spec.Smoothness);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                if (spec.EmissionColor.maxColorComponent > 0f)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", spec.EmissionColor);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else
                {
                    material.DisableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.black);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        private static object EnsurePrototypeUnitPrefabVisuals(MaterialSet materials)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUnitPrefabPath);
            if (prefabAsset == null)
            {
                throw new InvalidOperationException($"Prototype unit prefab was not found at '{PrototypeUnitPrefabPath}'.");
            }

            var prefabRoot = PrefabUtility.LoadPrefabContents(PrototypeUnitPrefabPath);
            try
            {
                var tacticalUnit = prefabRoot.GetComponent<TacticalUnit>();
                if (tacticalUnit == null)
                {
                    throw new InvalidOperationException($"Prototype unit prefab '{PrototypeUnitPrefabPath}' must contain a {nameof(TacticalUnit)} component.");
                }

                var bodyRenderer = FindRequiredChildRenderer(prefabRoot.transform, BodyChildName);
                var teamMarkerRenderer = FindRequiredChildRenderer(prefabRoot.transform, TeamMarkerChildName);
                var visualView = prefabRoot.GetComponent<UnitTeamVisualView>();
                var visualViewCreated = false;
                if (visualView == null)
                {
                    visualView = prefabRoot.AddComponent<UnitTeamVisualView>();
                    visualViewCreated = true;
                }

                visualView.Configure(
                    tacticalUnit,
                    bodyRenderer,
                    teamMarkerRenderer,
                    materials.PlayerTeam,
                    materials.EnemyTeam,
                    materials.PlayerTeamMarker,
                    materials.EnemyTeamMarker);
                visualView.ApplyTeamVisuals();

                ConfigureTeamMarker(teamMarkerRenderer, materials.PlayerTeamMarker);

                var activeMarker = EnsureMarker(
                    prefabRoot.transform,
                    ActiveMarkerChildName,
                    new Vector3(0f, 0.045f, 0f),
                    new Vector3(1.38f, 0.025f, 1.38f),
                    materials.ActiveMarker);
                var reactionMarker = EnsureMarker(
                    prefabRoot.transform,
                    ReactionMarkerChildName,
                    new Vector3(0f, 0.085f, 0f),
                    new Vector3(0.96f, 0.03f, 0.96f),
                    materials.ReactionMarker);

                ConfigureUnitHighlightView(prefabRoot, tacticalUnit, activeMarker, reactionMarker);

                EditorUtility.SetDirty(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrototypeUnitPrefabPath, out var savedSuccessfully);
                if (!savedSuccessfully)
                {
                    throw new InvalidOperationException($"Failed to save polished prototype unit prefab at '{PrototypeUnitPrefabPath}'.");
                }

                return new
                {
                    path = PrototypeUnitPrefabPath,
                    teamVisualViewCreated = visualViewCreated,
                    bodyRenderer = GetPrefabPath(bodyRenderer.gameObject),
                    teamMarkerRenderer = GetPrefabPath(teamMarkerRenderer.gameObject),
                    activeMarker = GetPrefabPath(activeMarker.Transform.gameObject),
                    reactionMarker = GetPrefabPath(reactionMarker.Transform.gameObject)
                };
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static Renderer FindRequiredChildRenderer(Transform prefabRoot, string childName)
        {
            var child = prefabRoot.Find(childName);
            if (child == null)
            {
                throw new InvalidOperationException($"Prototype unit prefab is missing child '{childName}'.");
            }

            var renderer = child.GetComponent<Renderer>();
            if (renderer == null)
            {
                throw new InvalidOperationException($"Prototype unit prefab child '{childName}' is missing a renderer.");
            }

            return renderer;
        }

        private static MarkerInfo EnsureMarker(
            Transform parent,
            string markerName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            var marker = parent.Find(markerName);
            if (marker == null)
            {
                var markerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                markerObject.name = markerName;
                markerObject.transform.SetParent(parent, false);
                marker = markerObject.transform;
            }

            marker.localPosition = localPosition;
            marker.localRotation = Quaternion.identity;
            marker.localScale = localScale;
            marker.gameObject.SetActive(false);

            var collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                EditorUtility.SetDirty(renderer);
            }

            EditorUtility.SetDirty(marker.gameObject);
            return new MarkerInfo(marker, renderer);
        }

        private static void ConfigureTeamMarker(Renderer teamMarkerRenderer, Material material)
        {
            if (teamMarkerRenderer == null)
            {
                return;
            }

            teamMarkerRenderer.sharedMaterial = material;
            teamMarkerRenderer.shadowCastingMode = ShadowCastingMode.Off;
            teamMarkerRenderer.receiveShadows = false;
            EditorUtility.SetDirty(teamMarkerRenderer);
        }

        private static void ConfigureUnitHighlightView(
            GameObject prefabRoot,
            TacticalUnit tacticalUnit,
            MarkerInfo activeMarker,
            MarkerInfo reactionMarker)
        {
            var highlightView = prefabRoot.GetComponent<UnitHighlightView>();
            if (highlightView == null)
            {
                highlightView = prefabRoot.AddComponent<UnitHighlightView>();
            }

            var serializedObject = new SerializedObject(highlightView);
            serializedObject.Update();
            SetObjectReference(serializedObject, "tacticalUnit", tacticalUnit);
            SetObjectReference(serializedObject, "activeMarker", activeMarker.Transform);
            SetObjectReference(serializedObject, "activeMarkerRenderer", activeMarker.Renderer);
            SetColor(serializedObject, "activeColor", new Color(1f, 0.88f, 0.05f, 1f));
            SetVector3(serializedObject, "activeMarkerLocalPosition", new Vector3(0f, 0.045f, 0f));
            SetVector3(serializedObject, "activeMarkerLocalScale", new Vector3(1.38f, 0.025f, 1.38f));
            SetObjectReference(serializedObject, "reactionMarker", reactionMarker.Transform);
            SetObjectReference(serializedObject, "reactionMarkerRenderer", reactionMarker.Renderer);
            SetColor(serializedObject, "reactionColor", new Color(0.05f, 0.95f, 1f, 1f));
            SetVector3(serializedObject, "reactionMarkerLocalPosition", new Vector3(0f, 0.085f, 0f));
            SetVector3(serializedObject, "reactionMarkerLocalScale", new Vector3(0.96f, 0.03f, 0.96f));
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(highlightView);
        }

        private static object ApplySceneVisuals(string scenePath)
        {
            var scene = OpenOrUseScene(scenePath);
            var light = EnsureDirectionalLight(scene, out var lightCreated);
            ConfigureDirectionalLight(light);
            ConfigureAmbientLighting();

            var gridManager = FindComponentInScene<GridManager>(scene);
            var terrainView = FindComponentInScene<GridTerrainView>(scene);
            if (terrainView != null)
            {
                ConfigureTerrainTileSpacing(terrainView);
            }

            var camera = FindComponentInScene<Camera>(scene);
            var cameraController = camera != null ? camera.GetComponent<TacticalCameraController>() : null;
            if (camera != null)
            {
                ConfigureCamera(camera, cameraController, gridManager);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException($"Failed to save scene '{scene.path}' after visual polish.");
            }

            return new
            {
                scene = scene.path,
                directionalLight = light != null ? GetScenePath(light.gameObject) : null,
                directionalLightCreated = lightCreated,
                ambientMode = RenderSettings.ambientMode.ToString(),
                ambientColor = ToColorSummary(RenderSettings.ambientLight),
                camera = camera != null ? GetScenePath(camera.gameObject) : null,
                cameraYaw = cameraController != null ? Math.Round(cameraController.YawDegrees, 3) : 0,
                cameraPitch = cameraController != null ? Math.Round(cameraController.PitchDegrees, 3) : 0,
                cameraZoom = cameraController != null ? Math.Round(cameraController.ZoomDistance, 3) : 0,
                terrainView = terrainView != null ? GetScenePath(terrainView.gameObject) : null,
                tileHorizontalScaleFactor = terrainView != null ? terrainView.TileHorizontalScaleFactor : 0f
            };
        }

        private static Scene OpenOrUseScene(string scenePath)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid() && string.Equals(activeScene.path, scenePath, StringComparison.Ordinal))
            {
                return activeScene;
            }

            if (activeScene.IsValid() && activeScene.isDirty)
            {
                throw new InvalidOperationException(
                    $"Active scene '{activeScene.path}' has unsaved changes. Save it before opening '{scenePath}'.");
            }

            return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        private static Light EnsureDirectionalLight(Scene scene, out bool created)
        {
            var light = FindNamedLight(scene, "Directional Light") ?? FindFirstDirectionalLight(scene);
            if (light != null)
            {
                created = false;
                light.gameObject.name = "Directional Light";
                return light;
            }

            var lightObject = new GameObject("Directional Light");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            light = lightObject.AddComponent<Light>();
            created = true;
            return light;
        }

        private static Light FindNamedLight(Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!string.Equals(root.name, objectName, StringComparison.Ordinal))
                {
                    continue;
                }

                var light = root.GetComponent<Light>();
                if (light != null)
                {
                    return light;
                }
            }

            return null;
        }

        private static Light FindFirstDirectionalLight(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var lights = root.GetComponentsInChildren<Light>(includeInactive: true);
                foreach (var light in lights)
                {
                    if (light != null && light.type == LightType.Directional)
                    {
                        return light;
                    }
                }
            }

            return null;
        }

        private static void ConfigureDirectionalLight(Light light)
        {
            if (light == null)
            {
                return;
            }

            light.type = LightType.Directional;
            light.color = new Color(1f, 0.94f, 0.82f, 1f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.62f;
            light.gameObject.SetActive(true);
            light.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(50f, -35f, 0f));
            EditorUtility.SetDirty(light);
            EditorUtility.SetDirty(light.gameObject);
        }

        private static void ConfigureAmbientLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.38f, 0.42f, 0.48f, 1f);
            RenderSettings.subtractiveShadowColor = new Color(0.20f, 0.22f, 0.28f, 1f);
            RenderSettings.fog = false;
        }

        private static void ConfigureTerrainTileSpacing(GridTerrainView terrainView)
        {
            var serializedObject = new SerializedObject(terrainView);
            serializedObject.Update();
            SetFloat(serializedObject, "tileHorizontalScaleFactor", TileHorizontalScaleFactor);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(terrainView);
        }

        private static void ConfigureCamera(
            Camera camera,
            TacticalCameraController cameraController,
            GridManager gridManager)
        {
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 120f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 1f);
            EditorUtility.SetDirty(camera);

            if (cameraController == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(cameraController);
            serializedObject.Update();
            SetObjectReference(serializedObject, "gridManager", gridManager);
            SetBool(serializedObject, "frameGridOnStart", true);
            SetFloat(serializedObject, "yawDegrees", 42f);
            SetFloat(serializedObject, "pitchDegrees", 58f);
            SetFloat(serializedObject, "minimumPitchDegrees", 35f);
            SetFloat(serializedObject, "maximumPitchDegrees", 72f);
            SetFloat(serializedObject, "minimumZoomDistance", 4f);
            SetFloat(serializedObject, "maximumZoomDistance", 30f);
            SetFloat(serializedObject, "framePaddingCells", 2.25f);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            if (gridManager != null && gridManager.RebuildMap())
            {
                cameraController.TryFrameGrid();
            }
            else
            {
                cameraController.SetView(new Vector3(3.5f, 0.5f, 3.5f), 42f, 58f, 15f);
            }

            EditorUtility.SetDirty(cameraController);
            EditorUtility.SetDirty(cameraController.gameObject);
        }

        private static T FindComponentInScene<T>(Scene scene)
            where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(includeInactive: true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on '{serializedObject.targetObject.name}'.");
            }

            property.objectReferenceValue = value;
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on '{serializedObject.targetObject.name}'.");
            }

            property.boolValue = value;
        }

        private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on '{serializedObject.targetObject.name}'.");
            }

            property.floatValue = value;
        }

        private static void SetColor(SerializedObject serializedObject, string propertyName, Color value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on '{serializedObject.targetObject.name}'.");
            }

            property.colorValue = value;
        }

        private static void SetVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on '{serializedObject.targetObject.name}'.");
            }

            property.vector3Value = value;
        }

        private static string GetScenePath(GameObject gameObject)
        {
            var path = gameObject.name;
            var current = gameObject.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static string GetPrefabPath(GameObject gameObject)
        {
            return GetScenePath(gameObject);
        }

        private static object ToColorSummary(Color color)
        {
            return new
            {
                r = Math.Round(color.r, 3),
                g = Math.Round(color.g, 3),
                b = Math.Round(color.b, 3),
                a = Math.Round(color.a, 3)
            };
        }

        public static string[] GetTrackedMaterialPaths()
        {
            var paths = new string[MaterialSpecs.Length];
            for (var i = 0; i < MaterialSpecs.Length; i++)
            {
                paths[i] = MaterialSpecs[i].Path;
            }

            return paths;
        }

        private readonly struct MaterialSpec
        {
            public MaterialSpec(string path, string name, Color baseColor, float smoothness, Color emissionColor)
            {
                Path = path;
                Name = name;
                BaseColor = baseColor;
                Smoothness = smoothness;
                EmissionColor = emissionColor;
            }

            public string Path { get; }

            public string Name { get; }

            public Color BaseColor { get; }

            public float Smoothness { get; }

            public Color EmissionColor { get; }
        }

        private sealed class MaterialSet
        {
            public MaterialSet(
                IReadOnlyDictionary<string, Material> materialsByPath,
                IReadOnlyList<string> createdPaths,
                IReadOnlyList<string> updatedPaths)
            {
                MaterialsByPath = materialsByPath;
                CreatedPaths = createdPaths;
                UpdatedPaths = updatedPaths;
            }

            public IReadOnlyDictionary<string, Material> MaterialsByPath { get; }

            public IReadOnlyList<string> CreatedPaths { get; }

            public IReadOnlyList<string> UpdatedPaths { get; }

            public Material PlayerTeam
            {
                get { return Get("Assets/Materials/PrototypeTeamPlayer.mat"); }
            }

            public Material EnemyTeam
            {
                get { return Get("Assets/Materials/PrototypeTeamEnemy.mat"); }
            }

            public Material PlayerTeamMarker
            {
                get { return Get("Assets/Materials/PrototypeTeamPlayerMarker.mat"); }
            }

            public Material EnemyTeamMarker
            {
                get { return Get("Assets/Materials/PrototypeTeamEnemyMarker.mat"); }
            }

            public Material ActiveMarker
            {
                get { return Get("Assets/Materials/PrototypeActiveMarker.mat"); }
            }

            public Material ReactionMarker
            {
                get { return Get("Assets/Materials/PrototypeReactionMarker.mat"); }
            }

            public object ToSummary()
            {
                return new
                {
                    materialCount = MaterialsByPath.Count,
                    createdCount = CreatedPaths.Count,
                    updatedCount = UpdatedPaths.Count,
                    createdPaths = CreatedPaths,
                    updatedPaths = UpdatedPaths,
                    playerTeam = AssetDatabase.GetAssetPath(PlayerTeam),
                    enemyTeam = AssetDatabase.GetAssetPath(EnemyTeam),
                    activeMarker = AssetDatabase.GetAssetPath(ActiveMarker),
                    reactionMarker = AssetDatabase.GetAssetPath(ReactionMarker)
                };
            }

            private Material Get(string path)
            {
                if (!MaterialsByPath.TryGetValue(path, out var material) || material == null)
                {
                    throw new InvalidOperationException($"Expected material '{path}' was not created.");
                }

                return material;
            }
        }

        private readonly struct MarkerInfo
        {
            public MarkerInfo(Transform transform, Renderer renderer)
            {
                Transform = transform;
                Renderer = renderer;
            }

            public Transform Transform { get; }

            public Renderer Renderer { get; }
        }
    }
}
