using Session.Player.Components;
using UnityEngine;

namespace Session.Items.Modifiers
{
    [CreateAssetMenu(fileName = "Gun", menuName = "Item/Gun", order = 1)]
    public class GunWithMagazineModifier : GunModifierBase
    {
        protected override void ReloadAmmo(ItemComponent itemComponents)
        {
            GunStatusComponent gunStatus = itemComponents.gunStatus;
            var addAmount = (ushort) (m_MagSize - gunStatus.ammoInMag);
            if (addAmount > gunStatus.ammoInReserve)
                addAmount = gunStatus.ammoInReserve;
            gunStatus.ammoInMag.Value += addAmount;
            gunStatus.ammoInReserve.Value -= addAmount;
        }
    }
}