using UnityEngine;
namespace StarterAssets
{
    public static class DomainInteractionState
    {
        public static bool IsModal { get; set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){IsModal=false;}
    }
}
