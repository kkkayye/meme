using NUnit.Framework;
using RuneArena.Combat;
using UnityEngine;

namespace RuneArena.Tests.EditMode
{
    public sealed class RigBoneMapTests
    {
        private static Transform Chain(Transform parent, params string[] names)
        {
            Transform current = parent;
            for (int i = 0; i < names.Length; i++)
            {
                var go = new GameObject(names[i]);
                go.transform.SetParent(current, false);
                current = go.transform;
            }
            return current;
        }

        [Test]
        public void ResolvesTripoNames()
        {
            var root = new GameObject("Armature");
            Transform hip = Chain(root.transform, "Root", "Hip");
            Chain(hip, "Pelvis", "L_Thigh", "L_Calf", "L_Foot");
            Chain(hip, "R_Thigh", "R_ThighTwist01");
            Chain(hip, "Waist", "Spine01", "Spine02", "L_Clavicle", "L_Upperarm", "L_Forearm", "L_Hand");
            RigBoneMap map = RigBoneMap.Build(root.transform);
            Assert.IsTrue(map.IsValid);
            Assert.AreEqual("Hip", map.Hips.name);
            Assert.AreEqual("Spine01", map.Spine.name);
            Assert.AreEqual("Spine02", map.Chest.name);
            Assert.AreEqual("L_Thigh", map.LeftUpperLeg.name);
            Assert.AreEqual("L_Calf", map.LeftLowerLeg.name);
            Assert.AreEqual("R_Thigh", map.RightUpperLeg.name);
            Assert.AreEqual("L_Upperarm", map.LeftUpperArm.name);
            Assert.AreEqual("L_Hand", map.LeftHand.name);
            Assert.IsNull(map.RightUpperArm);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ResolvesMixamoNames()
        {
            var root = new GameObject("Model");
            Transform hips = Chain(root.transform, "mixamorig:Hips");
            Chain(hips, "mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Neck", "mixamorig:Head");
            Chain(hips, "mixamorig:LeftUpLeg", "mixamorig:LeftLeg", "mixamorig:LeftFoot");
            Chain(hips, "mixamorig:RightArm", "mixamorig:RightForeArm", "mixamorig:RightHand");
            RigBoneMap map = RigBoneMap.Build(root.transform);
            Assert.IsTrue(map.IsValid);
            Assert.AreEqual("mixamorig:Hips", map.Hips.name);
            Assert.AreEqual("mixamorig:Spine1", map.Chest.name);
            Assert.AreEqual("mixamorig:LeftLeg", map.LeftLowerLeg.name);
            Assert.AreEqual("mixamorig:RightForeArm", map.RightLowerArm.name);
            Assert.AreEqual(11, map.OrderedBones().Count);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void UnriggedHierarchyIsInvalid()
        {
            var root = new GameObject("Model");
            Chain(root.transform, "mesh", "part1");
            Assert.IsFalse(RigBoneMap.Build(root.transform).IsValid);
            Object.DestroyImmediate(root);
        }
    }
}
