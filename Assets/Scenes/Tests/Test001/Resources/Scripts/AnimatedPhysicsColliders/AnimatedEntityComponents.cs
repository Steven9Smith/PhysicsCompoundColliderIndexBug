using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Animation.Hybrid;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Collections;

public class AnimatedEntityComponents : MonoBehaviour, IConvertGameObjectToEntity
{
    public RigComponent rig = null;
    public Transform HipTransform;

    private RigComponent GetRigComponent()
    {
        return this.gameObject.GetComponent<RigComponent>();
    }

    private void OnEnable()
    {
        if (rig == null) rig = GetRigComponent();
    }

    void Start()
    {
        if (rig == null) rig = GetRigComponent();
    }

    void GetData(RigComponent rigComponent, out Transform[] children, out PhysicsShapeAuthoring[] childrenShapes)
    {
        children = new Transform[rigComponent.Bones.Length];
        childrenShapes = new PhysicsShapeAuthoring[rigComponent.Bones.Length];
        for (int i = 0; i < rigComponent.Bones.Length; i++)
        {
            children[i] = rigComponent.Bones[i];
            childrenShapes[i] = children[i].GetComponent<PhysicsShapeAuthoring>();
        }
    }

    public int GetNumberOfValidShapes(PhysicsShapeAuthoring[] shapes)
    {
        int count = 0;
        foreach (var shape in shapes)
            if (shape != null && shape.enabled)
                count++;
        return count;
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        if (rig != null)
        {
            var buffer = dstManager.AddBuffer<PhysicsColliderFollowEntityData>(entity);
            GetData(rig, out Transform[] children, out var shapes);

            if (children.Length > 0)
            {
                int numberOfValidShapes = GetNumberOfValidShapes(shapes);
                for (int i = 0; i < children.Length; i++)
                {
                    if (shapes[i] != null && shapes[i].enabled)
                    {
                        var component = children[i].gameObject.AddComponent<AnimatedEntityComponent>();
                        component.RigComponentTransform = this.transform;
                        component.Hips = HipTransform;
                        component.PhysicsShapeCompoundColliderOverride = children[i].childCount > 1 ? true : false;
                        component.index = i;
                        children[i].gameObject.AddComponent<ConvertToEntity>();
                    }
                }
            }
            else Debug.LogWarning("failed to find any colliders");
        }
        else UnityEngine.Debug.LogWarning("The GameObject \"" + name + "\" does not have a RigCOmponent");
    }
}
