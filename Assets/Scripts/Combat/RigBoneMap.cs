using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Resolves the humanoid bones of an imported skeleton by name (Tripo, Mixamo and common naming schemes) so procedural animation can drive any rig.</summary>
    public sealed class RigBoneMap
    {
        public Transform Hips;
        public Transform Spine;
        public Transform Chest;
        public Transform Neck;
        public Transform Head;
        public Transform LeftUpperLeg;
        public Transform LeftLowerLeg;
        public Transform LeftFoot;
        public Transform RightUpperLeg;
        public Transform RightLowerLeg;
        public Transform RightFoot;
        public Transform LeftUpperArm;
        public Transform LeftLowerArm;
        public Transform LeftHand;
        public Transform RightUpperArm;
        public Transform RightLowerArm;
        public Transform RightHand;

        public bool IsValid => Hips != null && (LeftUpperLeg != null || LeftUpperArm != null || Spine != null);

        private static readonly string[] HipsNames = { "hips", "hip", "pelvis" };
        private static readonly string[] SpineNames = { "spine01", "spine1", "spine", "waist" };
        private static readonly string[] ChestNames = { "spine02", "spine2", "chest", "spine03", "spine3", "upperchest" };
        private static readonly string[] NeckNames = { "neck", "necktwist01", "neck1" };
        private static readonly string[] HeadNames = { "head" };
        private static readonly string[] LeftUpperLegNames = { "l_thigh", "leftupleg", "thigh_l", "l_upleg", "leftupperleg", "upleg_l", "thighl", "lthigh" };
        private static readonly string[] LeftLowerLegNames = { "l_calf", "leftleg", "calf_l", "l_leg", "leftlowerleg", "leg_l", "lcalf", "l_shin", "shin_l" };
        private static readonly string[] LeftFootNames = { "l_foot", "leftfoot", "foot_l", "lfoot" };
        private static readonly string[] RightUpperLegNames = { "r_thigh", "rightupleg", "thigh_r", "r_upleg", "rightupperleg", "upleg_r", "thighr", "rthigh" };
        private static readonly string[] RightLowerLegNames = { "r_calf", "rightleg", "calf_r", "r_leg", "rightlowerleg", "leg_r", "rcalf", "r_shin", "shin_r" };
        private static readonly string[] RightFootNames = { "r_foot", "rightfoot", "foot_r", "rfoot" };
        private static readonly string[] LeftUpperArmNames = { "l_upperarm", "leftarm", "upperarm_l", "l_arm", "leftupperarm", "arm_l", "lupperarm" };
        private static readonly string[] LeftLowerArmNames = { "l_forearm", "leftforearm", "forearm_l", "leftlowerarm", "lforearm", "lowerarm_l" };
        private static readonly string[] LeftHandNames = { "l_hand", "lefthand", "hand_l", "lhand" };
        private static readonly string[] RightUpperArmNames = { "r_upperarm", "rightarm", "upperarm_r", "r_arm", "rightupperarm", "arm_r", "rupperarm" };
        private static readonly string[] RightLowerArmNames = { "r_forearm", "rightforearm", "forearm_r", "rightlowerarm", "rforearm", "lowerarm_r" };
        private static readonly string[] RightHandNames = { "r_hand", "righthand", "hand_r", "rhand" };

        /// <summary>Scans every descendant of root and fills the map. Never throws; check IsValid.</summary>
        public static RigBoneMap Build(Transform root)
        {
            var map = new RigBoneMap();
            if (root == null) return map;
            var byName = new Dictionary<string, Transform>();
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string key = Normalize(all[i].name);
                if (key.Contains("twist") || key.Contains("roll") || key.Contains("_end") || byName.ContainsKey(key)) continue;
                byName[key] = all[i];
            }
            map.Hips = Find(byName, HipsNames);
            map.Spine = Find(byName, SpineNames);
            map.Chest = Find(byName, ChestNames);
            map.Neck = Find(byName, NeckNames);
            map.Head = Find(byName, HeadNames);
            map.LeftUpperLeg = Find(byName, LeftUpperLegNames);
            map.LeftLowerLeg = Find(byName, LeftLowerLegNames);
            map.LeftFoot = Find(byName, LeftFootNames);
            map.RightUpperLeg = Find(byName, RightUpperLegNames);
            map.RightLowerLeg = Find(byName, RightLowerLegNames);
            map.RightFoot = Find(byName, RightFootNames);
            map.LeftUpperArm = Find(byName, LeftUpperArmNames);
            map.LeftLowerArm = Find(byName, LeftLowerArmNames);
            map.LeftHand = Find(byName, LeftHandNames);
            map.RightUpperArm = Find(byName, RightUpperArmNames);
            map.RightLowerArm = Find(byName, RightLowerArmNames);
            map.RightHand = Find(byName, RightHandNames);
            if (map.Spine == map.Hips) map.Spine = null;
            return map;
        }

        /// <summary>Every mapped bone, parents before children (hips, torso, then limbs).</summary>
        public List<Transform> OrderedBones()
        {
            var list = new List<Transform>();
            Add(list, Hips); Add(list, Spine); Add(list, Chest); Add(list, Neck); Add(list, Head);
            Add(list, LeftUpperLeg); Add(list, LeftLowerLeg); Add(list, LeftFoot);
            Add(list, RightUpperLeg); Add(list, RightLowerLeg); Add(list, RightFoot);
            Add(list, LeftUpperArm); Add(list, LeftLowerArm); Add(list, LeftHand);
            Add(list, RightUpperArm); Add(list, RightLowerArm); Add(list, RightHand);
            return list;
        }

        /// <summary>Lower-case, "mixamorig:" prefix and spaces / dots / dashes removed; underscores kept.</summary>
        public static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string n = name.ToLowerInvariant();
            int colon = n.LastIndexOf(':');
            if (colon >= 0) n = n.Substring(colon + 1);
            return n.Replace(" ", "").Replace(".", "").Replace("-", "");
        }

        private static Transform Find(Dictionary<string, Transform> byName, string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                if (byName.TryGetValue(candidates[i], out Transform t)) return t;
            }
            return null;
        }

        private static void Add(List<Transform> list, Transform t)
        {
            if (t != null && !list.Contains(t)) list.Add(t);
        }
    }
}
