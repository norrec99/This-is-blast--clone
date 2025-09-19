using UnityEngine;

[System.Serializable]
public class PlatformSlot
{
    public Transform anchor;
    [HideInInspector] public bool isOccupied;
    [HideInInspector] public ShooterPlacementController occupant;
}

public class PlatformManager : MonoBehaviour
{
    [Header("Platform Slots (assign anchors left→right in world X)")]
    public PlatformSlot[] slots;

    public bool TryReserveLeftmostFreeSlot(ShooterPlacementController requester, out int slotIndex, out Transform targetAnchor)
    {
        slotIndex = -1;
        targetAnchor = null;

        if (slots == null || slots.Length == 0)
        {
            return false;
        }

        bool foundAny = false;
        float bestX = float.PositiveInfinity;
        int bestIndex = -1;

        for (int i = 0; i < slots.Length; i++)
        {
            PlatformSlot s = slots[i];

            if (s == null)
            {
                continue;
            }

            if (s.isOccupied)
            {
                continue;
            }

            if (s.anchor == null)
            {
                continue;
            }

            float x = s.anchor.position.x;

            if (!foundAny)
            {
                foundAny = true;
                bestX = x;
                bestIndex = i;
            }
            else
            {
                if (x < bestX)
                {
                    bestX = x;
                    bestIndex = i;
                }
            }
        }

        if (!foundAny)
        {
            return false;
        }

        slots[bestIndex].isOccupied = true;
        slots[bestIndex].occupant = requester;

        slotIndex = bestIndex;
        targetAnchor = slots[bestIndex].anchor;
        return true;
    }

    public void ReleaseSlotOwnedBy(ShooterPlacementController requester)
    {
        if (slots == null || slots.Length == 0)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            PlatformSlot s = slots[i];

            if (s == null)
            {
                continue;
            }

            if (!s.isOccupied)
            {
                continue;
            }

            if (s.occupant == requester)
            {
                s.isOccupied = false;
                s.occupant = null;
                return;
            }
        }
    }

    public bool HasFreeSlot()
    {
        if (slots == null || slots.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            PlatformSlot s = slots[i];

            if (s == null)
            {
                continue;
            }

            if (!s.isOccupied)
            {
                return true;
            }
        }

        return false;
    }
}
