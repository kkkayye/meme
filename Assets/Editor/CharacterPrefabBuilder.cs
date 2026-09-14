using System.Collections.Generic;
using System.IO;
using RuneArena.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RuneArena.Editor
{
    /// <summary>Turns each Assets/Art/Tripo/&lt;id&gt;/ folder into Assets/Resources/Characters/&lt;id&gt;.prefab with an Animator and a generated controller (Locomotion blend tree, Attack, Cast, Dash, Hurt, Dead). Menu: RuneArena/Build Character Prefabs, or -executeMethod RuneArena.Editor.CharacterPrefabBuilder.BuildAll.</summary>
    public static class CharacterPrefabBuilder
    {
        private const string ArtFolder = "Assets/Art/Tripo";
        private const string GeneratedFolder = "Assets/Art/Generated";
        private const string ResourcesFolder = "Assets/Resources/Characters";

        private sealed class Roles
        {
            public AnimationClip Idle;
            public AnimationClip Walk;
            public AnimationClip Run;
            public AnimationClip Attack;
            public AnimationClip Cast;
            public AnimationClip Dash;
            public AnimationClip Hurt;
            public AnimationClip Dead;
        }

        private static readonly string[] AttackNames = { "slash", "shoot", "fire", "chop" };
        private static readonly string[] CastNames = { "cast_a_spell", "cast", "chop", "slash", "shoot" };
        private static readonly string[] DashNames = { "dive", "jump", "run" };
        private static readonly string[] DeadNames = { "fall", "defeat", "death", "die" };

        [MenuItem("RuneArena/Build Character Prefabs")]
        public static void BuildAll()
        {
            if (!Directory.Exists(ArtFolder))
            {
                Debug.LogWarning("CharacterPrefabBuilder: no " + ArtFolder + " folder yet.");
                return;
            }
            EnsureFolder(GeneratedFolder);
            EnsureFolder(ResourcesFolder);
            int built = 0;
            foreach (string dir in Directory.GetDirectories(ArtFolder))
            {
                string id = Path.GetFileName(dir);
                if (Build(id, dir.Replace('\\', '/'))) built++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CharacterPrefabBuilder: built " + built + " character prefab(s).");
        }

        private static bool Build(string id, string folder)
        {
            GameObject model = FindModel(folder, id);
            if (model == null)
            {
                Debug.LogWarning("CharacterPrefabBuilder: no rig/model FBX in " + folder);
                return false;
            }
            List<AnimationClip> clips = CollectClips(folder);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = id + "_model";
            try
            {
                if (instance.GetComponent<CharacterVisualConfig>() == null) instance.AddComponent<CharacterVisualConfig>();
                if (clips.Count > 0) AttachAnimator(instance, id, folder, clips);
                else RemoveAnimator(instance);
                string prefabPath = ResourcesFolder + "/" + id + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Debug.Log("CharacterPrefabBuilder: " + prefabPath + " (" + clips.Count + " clips)");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static GameObject FindModel(string folder, string id)
        {
            string[] candidates = { id + "_rig.fbx", id + "_model.fbx", id + ".fbx" };
            for (int i = 0; i < candidates.Length; i++)
            {
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + candidates[i]);
                if (go != null) return go;
            }
            foreach (string file in Directory.GetFiles(folder, "*.fbx"))
            {
                if (file.Contains("_anim")) continue;
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
                if (go != null) return go;
            }
            return null;
        }

        private static List<AnimationClip> CollectClips(string folder)
        {
            var clips = new List<AnimationClip>();
            foreach (string file in Directory.GetFiles(folder, "*.fbx"))
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(file.Replace('\\', '/'));
                for (int i = 0; i < assets.Length; i++)
                {
                    var clip = assets[i] as AnimationClip;
                    if (clip == null || clip.name.StartsWith("__preview__")) continue;
                    clips.Add(clip);
                }
            }
            return clips;
        }

        private static void AttachAnimator(GameObject instance, string id, string folder, List<AnimationClip> clips)
        {
            Roles roles = Assign(clips);
            AnimatorController controller = BuildController(id, roles);
            Animator animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        private static void RemoveAnimator(GameObject instance)
        {
            Animator animator = instance.GetComponent<Animator>();
            if (animator != null) Object.DestroyImmediate(animator);
        }

        private static Roles Assign(List<AnimationClip> clips)
        {
            var roles = new Roles
            {
                Idle = Find(clips, "idle"),
                Walk = Find(clips, "walk"),
                Run = Find(clips, "run"),
                Hurt = Find(clips, "hurt"),
                Dead = FindAny(clips, DeadNames),
                Dash = FindAny(clips, DashNames),
                Attack = FindAny(clips, AttackNames),
                Cast = FindAny(clips, CastNames)
            };
            if (roles.Attack != null && roles.Cast == roles.Attack)
            {
                AnimationClip alternative = FindAnyExcept(clips, CastNames, roles.Attack);
                if (alternative != null) roles.Cast = alternative;
            }
            if (roles.Run == null) roles.Run = roles.Walk ?? roles.Idle;
            if (roles.Idle == null) roles.Idle = roles.Run;
            return roles;
        }

        private static AnimationClip Find(List<AnimationClip> clips, string needle)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i].name.ToLowerInvariant().Contains(needle)) return clips[i];
            }
            return null;
        }

        private static AnimationClip FindAny(List<AnimationClip> clips, string[] needles)
        {
            for (int n = 0; n < needles.Length; n++)
            {
                AnimationClip clip = Find(clips, needles[n]);
                if (clip != null) return clip;
            }
            return null;
        }

        private static AnimationClip FindAnyExcept(List<AnimationClip> clips, string[] needles, AnimationClip except)
        {
            for (int n = 0; n < needles.Length; n++)
            {
                for (int i = 0; i < clips.Count; i++)
                {
                    if (clips[i] != except && clips[i].name.ToLowerInvariant().Contains(needles[n])) return clips[i];
                }
            }
            return null;
        }

        private static AnimatorController BuildController(string id, Roles roles)
        {
            string path = GeneratedFolder + "/" + id + ".controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(UnitAnimator.SpeedParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(UnitAnimator.AttackSpeedParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(UnitAnimator.AttackParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(UnitAnimator.CastParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(UnitAnimator.DashParam, AnimatorControllerParameterType.Bool);
            controller.AddParameter(UnitAnimator.DeadParam, AnimatorControllerParameterType.Bool);
            controller.AddParameter(UnitAnimator.HurtParam, AnimatorControllerParameterType.Trigger);
            SetDefault(controller, UnitAnimator.AttackSpeedParam, 1f);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState locomotion = BuildLocomotion(controller, sm, roles);
            sm.defaultState = locomotion;
            AddAction(controller, sm, locomotion, "Attack", roles.Attack ?? roles.Cast, UnitAnimator.AttackParam, true);
            AddAction(controller, sm, locomotion, "Cast", roles.Cast ?? roles.Attack, UnitAnimator.CastParam, false);
            AddAction(controller, sm, locomotion, "Hurt", roles.Hurt, UnitAnimator.HurtParam, false);
            AddBoolState(sm, locomotion, "Dash", roles.Dash ?? roles.Run, UnitAnimator.DashParam, 1.2f);
            AddBoolState(sm, locomotion, "Dead", roles.Dead ?? roles.Hurt ?? roles.Idle, UnitAnimator.DeadParam, 1f);
            return controller;
        }

        private static void SetDefault(AnimatorController controller, string name, float value)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == name) parameters[i].defaultFloat = value;
            }
            controller.parameters = parameters;
        }

        private static AnimatorState BuildLocomotion(AnimatorController controller, AnimatorStateMachine sm, Roles roles)
        {
            AnimatorState state = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = UnitAnimator.SpeedParam;
            tree.useAutomaticThresholds = false;
            if (roles.Idle != null) tree.AddChild(roles.Idle, 0f);
            if (roles.Walk != null && roles.Walk != roles.Run) tree.AddChild(roles.Walk, 0.45f);
            if (roles.Run != null) tree.AddChild(roles.Run, 1f);
            return state;
        }

        /// <summary>Trigger-driven one-shot: AnyState → state (no exit time), state → locomotion at 90%.</summary>
        private static void AddAction(AnimatorController controller, AnimatorStateMachine sm, AnimatorState locomotion, string name, AnimationClip clip, string trigger, bool scaleByAttackSpeed)
        {
            if (clip == null) return;
            AnimatorState state = sm.AddState(name);
            state.motion = clip;
            if (scaleByAttackSpeed)
            {
                state.speedParameterActive = true;
                state.speedParameter = UnitAnimator.AttackSpeedParam;
            }
            AnimatorStateTransition enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.hasExitTime = false;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = true;
            enter.interruptionSource = TransitionInterruptionSource.Destination;
            AnimatorStateTransition exit = state.AddTransition(locomotion);
            exit.hasExitTime = true;
            exit.exitTime = 0.9f;
            exit.duration = 0.12f;
        }

        /// <summary>Bool-driven looping state: AnyState → state while the bool is true, back when it turns false.</summary>
        private static void AddBoolState(AnimatorStateMachine sm, AnimatorState locomotion, string name, AnimationClip clip, string parameter, float speed)
        {
            if (clip == null) return;
            AnimatorState state = sm.AddState(name);
            state.motion = clip;
            state.speed = speed;
            AnimatorStateTransition enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, parameter);
            enter.hasExitTime = false;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = false;
            AnimatorStateTransition exit = state.AddTransition(locomotion);
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, parameter);
            exit.hasExitTime = false;
            exit.duration = 0.15f;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
