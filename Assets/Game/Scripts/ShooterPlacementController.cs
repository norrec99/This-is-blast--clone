using UnityEngine;
using DG.Tweening;

public class ShooterPlacementController : MonoBehaviour
{
    [Header("Refs")]
    public PlatformManager platformManager;
    public Shooter shooter;

    [Header("Movement")]
    public float moveDuration = 0.35f;
    public Ease moveEase = Ease.InOutSine;
    public bool startAutoFireAfterDock = true;

    private bool isMoving;
    private bool hasDocked;
    private int reservedSlotIndex = -1;
    private Collider clickCollider;
    private Tween moveTween;

    private void Awake()
    {
        if (shooter == null)
        {
            shooter = GetComponent<Shooter>();
        }

        clickCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        if (clickCollider != null)
        {
            clickCollider.enabled = true;
        }
    }

    private void OnDisable()
    {
        if (moveTween != null)
        {
            if (moveTween.IsActive())
            {
                moveTween.Kill(false);
            }

            moveTween = null;
        }

        if (platformManager != null)
        {
            platformManager.ReleaseSlotOwnedBy(this);
        }
    }

    private void OnDestroy()
    {
        if (moveTween != null)
        {
            if (moveTween.IsActive())
            {
                moveTween.Kill(false);
            }

            moveTween = null;
        }

        if (platformManager != null)
        {
            platformManager.ReleaseSlotOwnedBy(this);
        }
    }

    private void OnMouseDown()
    {
        if (isMoving)
        {
            return;
        }

        if (hasDocked)
        {
            return;
        }

        if (platformManager == null)
        {
            return;
        }

        if (!platformManager.HasFreeSlot())
        {
            return;
        }

        Transform targetAnchor;
        bool isReserved = platformManager.TryReserveLeftmostFreeSlot(this, out reservedSlotIndex, out targetAnchor);

        if (!isReserved)
        {
            return;
        }

        if (clickCollider != null)
        {
            clickCollider.enabled = false;
        }

        StartDockTween(targetAnchor);
    }

    private void StartDockTween(Transform targetAnchor)
    {
        if (targetAnchor == null)
        {
            if (platformManager != null)
            {
                platformManager.ReleaseSlotOwnedBy(this);
            }

            return;
        }

        if (moveTween != null)
        {
            if (moveTween.IsActive())
            {
                moveTween.Kill(false);
            }

            moveTween = null;
        }

        isMoving = true;

        Vector3 endPos = targetAnchor.position;

        moveTween = transform
            .DOMove(endPos, moveDuration)
            .SetEase(moveEase)
            .OnComplete(() =>
            {
                OnDockArrived();
            });
    }

    private void OnDockArrived()
    {
        isMoving = false;
        hasDocked = true;
        moveTween = null;

        if (startAutoFireAfterDock)
        {
            if (shooter != null)
            {
                shooter.StartAutoFire();
            }
        }
    }
}
