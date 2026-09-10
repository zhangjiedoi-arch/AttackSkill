using UnityEngine;

namespace AttackSkill.UI
{
    /// <summary>挂在场景物体上：动态点走登记表；静态点优先用 Placement JSON。</summary>
    public sealed class MapMarkerBinder : MonoBehaviour
    {
        [SerializeField] string uid;
        [SerializeField] string typeId;
        [SerializeField] string mapId;
        [SerializeField] bool finished;

        public string Uid => string.IsNullOrEmpty(uid) ? "dyn_" + GetInstanceID() : uid;
        public string TypeId => typeId;
        public string MapId => mapId;
        public bool Finished => finished;

        void OnEnable()
        {
            MapMarkerRegistry.Register(this);
        }

        void OnDisable()
        {
            MapMarkerRegistry.Unregister(this);
        }
    }
}
