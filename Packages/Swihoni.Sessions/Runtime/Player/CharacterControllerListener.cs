using UnityEngine;

namespace Swihoni.Sessions.Player
{
    public class CharacterControllerListener : MonoBehaviour
    {
        public ControllerColliderHit CachedControllerHit { get; private set; } = new ControllerColliderHit();
        
        private void OnControllerColliderHit(ControllerColliderHit hit) => CachedControllerHit = hit;
    }
}