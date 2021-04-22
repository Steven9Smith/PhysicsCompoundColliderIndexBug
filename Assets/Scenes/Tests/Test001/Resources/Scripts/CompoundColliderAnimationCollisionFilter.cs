using System.Collections;
using Unity.Entities;
using Unity.Physics.Authoring;
using UnityEngine;


public class CompoundColliderAnimationCollisionFilter : MonoBehaviour, IConvertGameObjectToEntity
{
    public CompoundColliderAnimationCollisionFilterData.CollisionFilter Filter;
    public int index;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new CompoundColliderAnimationCollisionFilterData
        {
            BelongsTo = CompoundColliderAnimationCollisionFilterData.GetFilter(Filter),
            index = index
        });
    }

    void OnValidate()
    {
        var a = GetComponent<AnimatedEntityComponent>();
        index = a.index;
    }
}

public struct CompoundColliderAnimationCollisionFilterData : IComponentData
{
    public uint BelongsTo;
    public int index;
    public static uint GetFilter(CollisionFilter collisionFilter) { return (uint)(1 << (int)collisionFilter); }

    public enum CollisionFilter
    {
        Hip = 800,
        Spine0 = 801,
        Spine1 = 802,
        Spine2 = 803,
        Spine3 = 804,
        Spine4 = 805,
        Spine5 = 806,
        Spine6 = 807,
        Spine7 = 808,
        Spine8 = 809,
        Spine9 = 810,
        Neck0 = 811,
        Neck1 = 812,
        Neck2 = 813,
        Head = 814,
        Shoulder0 = 815,
        Shoulder1 = 816,
        Shoulder2 = 817,
        Shoulder3 = 818,
        UpperArm0 = 819,
        UpperArm1 = 820,
        UpperArm2 = 821,
        UpperArm3 = 822,
        ForeArm0 = 823,
        ForeArm1 = 824,
        ForeArm2 = 825,
        ForeArm3 = 826,
        Hip0 = 827,
        Hip1 = 828,
        Hip2 = 829,
        Hip3 = 830,
        UpperLeg0 = 831,
        UpperLeg1 = 832,
        UpperLeg2 = 833,
        UpperLeg3 = 834,
        LowerLeg0 = 835,
        LowerLeg1 = 836,
        LowerLeg2 = 837,
        LowerLeg3 = 838,
        Foot0 = 839,
        Foot1 = 840,
        Foot2 = 841,
        Foot3 = 842,
    }
}
