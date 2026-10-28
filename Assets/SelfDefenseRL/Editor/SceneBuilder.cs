using System.IO;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SelfDefenseRL.EditorTools
{
    /// <summary>
    /// Baut die Szenen automatisch auf: Menü "SelfDefenseRL" in der Unity-Menüleiste.
    /// </summary>
    public static class SceneBuilder
    {
        const string Root = "Assets/SelfDefenseRL";
        const string SceneFolder = Root + "/Scenes";
        const string MaterialFolder = Root + "/Materials";
        const float ArenaSpacing = 11f;

        [MenuItem("SelfDefenseRL/Trainings-Szene erstellen (16 Arenen)")]
        public static void BuildTraining() => Build("Training", 16, false);

        [MenuItem("SelfDefenseRL/Demo-Szene erstellen (1 Arena)")]
        public static void BuildDemo() => Build("Demo", 1, false);

        [MenuItem("SelfDefenseRL/Auswertungs-Szene erstellen (16 Arenen, CSV-Log, alle Kombinationen)")]
        public static void BuildEvaluation() => Build("Evaluation", 16, true);

        static void Build(string name, int arenas, bool csvLog)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var lineMat = GetOrCreateLineMaterial();
            var floorMat = GetOrCreateColorMaterial("Floor", new Color(0.82f, 0.82f, 0.78f));
            var wallMat = GetOrCreateColorMaterial("Wall", new Color(0.35f, 0.35f, 0.4f));
            var defMat = GetOrCreateColorMaterial("DefenderBody", new Color(0.2f, 0.45f, 0.95f));
            var attMat = GetOrCreateColorMaterial("AttackerBody", new Color(0.8f, 0.18f, 0.18f));
            var skinMat = GetOrCreateColorMaterial("Skin", new Color(0.93f, 0.76f, 0.62f));

            int cols = Mathf.CeilToInt(Mathf.Sqrt(arenas));
            for (int i = 0; i < arenas; i++)
            {
                var pos = new Vector3((i % cols) * ArenaSpacing, 0f, (i / cols) * ArenaSpacing);
                CreateArena(i, pos, lineMat, floorMat, wallMat, defMat, attMat, skinMat, csvLog);
            }

            // Live-Statistik im Game-Fenster
            new GameObject("StatsHUD").AddComponent<StatsHUD>();

            // Kamera
            var cam = Camera.main;
            if (cam != null)
            {
                if (arenas == 1)
                {
                    cam.transform.position = new Vector3(0f, 9f, -9f);
                    cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
                }
                else
                {
                    float mid = (cols - 1) * ArenaSpacing * 0.5f;
                    cam.transform.position = new Vector3(mid, cols * 12f, mid - cols * 9f);
                    cam.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
                }
            }

            Directory.CreateDirectory(SceneFolder);
            string path = $"{SceneFolder}/{name}.unity";
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.Refresh();
            Debug.Log($"[SelfDefenseRL] Szene gespeichert: {path} ({arenas} Arena(s))");
        }

        static void CreateArena(int index, Vector3 position, Material lineMat, Material floorMat, Material wallMat,
                                Material defMat, Material attMat, Material skinMat, bool csvLog)
        {
            var root = new GameObject($"Arena_{index:00}");
            root.transform.position = position;

            // Boden 8x8 m
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform, false);
            floor.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            // Wände
            const float h = 4f;
            CreateWall(root.transform, new Vector3(0f, 0.5f, h + 0.1f), new Vector3(8.4f, 1f, 0.2f), wallMat);
            CreateWall(root.transform, new Vector3(0f, 0.5f, -h - 0.1f), new Vector3(8.4f, 1f, 0.2f), wallMat);
            CreateWall(root.transform, new Vector3(h + 0.1f, 0.5f, 0f), new Vector3(0.2f, 1f, 8.4f), wallMat);
            CreateWall(root.transform, new Vector3(-h - 0.1f, 0.5f, 0f), new Vector3(0.2f, 1f, 8.4f), wallMat);

            // Menschliche Figuren (ohne Collider: die Kampflogik steckt in CombatSimulation)
            var defenderFig = CreateFigure("Defender", root.transform, new Vector3(0f, 0f, 0f), 0f, defMat, skinMat);
            var attackerFig = CreateFigure("Attacker", root.transform, new Vector3(0f, 0f, 1f), 180f, attMat, skinMat);
            var defender = defenderFig.gameObject;

            // Agent
            var agent = defender.AddComponent<DefenderAgent>();
            agent.MaxStep = 0; // Episodenende regelt die Simulation (MaxEpisodeTime)
            agent.logEpisodesToCsv = csvLog;
            agent.evaluationSweep = csvLog; // Auswertung: alle Kombinationen systematisch durchgehen
            agent.seed = 0;

            var bp = defender.GetComponent<BehaviorParameters>();
            bp.BehaviorName = "SelfDefense";
            bp.BrainParameters.VectorObservationSize = DefenderAgent.ObservationSize;
            bp.BrainParameters.NumStackedVectorObservations = 1;
            bp.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(EnumCounts.MoveActions, TechniqueLibrary.Count);

            var dr = defender.AddComponent<DecisionRequester>();
            dr.DecisionPeriod = 5;
            dr.TakeActionsBetweenDecisions = false;
            agent.ticksPerDecision = dr.DecisionPeriod;

            // Anzeige
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root.transform, false);
            var label = labelGo.AddComponent<TextMesh>();
            label.characterSize = 0.08f;
            label.fontSize = 48;
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.black;

            var lineGo = new GameObject("GripLine");
            lineGo.transform.SetParent(root.transform, false);
            var line = lineGo.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.startWidth = line.endWidth = 0.06f;
            line.sharedMaterial = lineMat;
            line.startColor = line.endColor = new Color(1f, 0.5f, 0f);
            line.enabled = false;

            var view = root.AddComponent<ArenaView>();
            view.agent = agent;
            view.defender = defenderFig;
            view.attacker = attackerFig;
            view.label = label;
            view.gripLine = line;
        }

        static void CreateWall(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = localPos;
            wall.transform.localScale = scale;
            Object.DestroyImmediate(wall.GetComponent<Collider>());
            wall.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static HumanoidFigure CreateFigure(string name, Transform parent, Vector3 localPos, float yaw, Material body, Material skin)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var fig = go.AddComponent<HumanoidFigure>();
            fig.Build(body, skin);
            return fig;
        }

        static Material GetOrCreateLineMaterial()
        {
            string path = $"{MaterialFolder}/GripLine.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            Directory.CreateDirectory(MaterialFolder);
            mat = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material GetOrCreateColorMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            Directory.CreateDirectory(MaterialFolder);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
