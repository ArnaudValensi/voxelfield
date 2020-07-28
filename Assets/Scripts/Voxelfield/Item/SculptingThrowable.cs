using Swihoni.Sessions;
using Swihoni.Sessions.Entities;
using Swihoni.Util.Math;
using UnityEngine;
using Voxelfield.Session;
using Voxels;

namespace Voxelfield.Item
{
    public class SculptingThrowable : ThrowableModifierBehavior
    {
        private static readonly Color32 Sand = new Color32(253, 255, 224, 255);

        protected override void JustPopped(in ModifyContext context)
        {
            var server = (ServerInjector) context.session.Injector;
            
            var center = (Position3Int) (transform.position + new Vector3 {y = m_Radius * 0.75f});
            var changes = new VoxelChange {color = Sand, magnitude = m_Radius, texture = VoxelTexture.Speckled, natural = false, form = VoxelVolumeForm.Cylindrical};
            server.EvaluateVoxelChange(center, changes);
        }
    }
}