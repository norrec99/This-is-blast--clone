using System.Collections;
using UnityEngine;

public class Shooter : MonoBehaviour
{
    [Header("References")]
    public Board board;
    public ProjectilePool projectilePool;
    public Transform muzzle;

    [Header("Cannon Config")]
    public BlockColor cannonColor = BlockColor.Red;
    public int ammoCount = 10;
    public bool destroyOnAmmoDepleted = true;

    [Header("Firing Pace")]
    public float projectileSpeed = 16.0f;    // how fast projectiles travel
    public float shotInterval = 0.15f;       // time between shots (does NOT wait for impact)
    public float collapseDuration = 0.12f;   // should match Board.DestroyAtSimul animation time
    public float zOffset = 0.0f;             // board on Z=0 -> keep 0

    [Header("Keys (optional)")]
    public KeyCode startAutoKey = KeyCode.F; // start firing until no targets or ammo=0
    public KeyCode stopAutoKey = KeyCode.S;  // stop auto firing
    public KeyCode fireOnceKey = KeyCode.Space; // single immediate shot (if a target exists)

    private Coroutine autoFireRoutine;
    private bool isAutoFiring;
    private int inFlightCount;

    // Reservations so we do not repeatedly target the same bottom cell while a shot is in flight.
    private bool[] reservedBottomTargets;

    private void Awake()
    {
        EnsureReservationsAllocated();
    }

    private void Update()
    {
        if (board == null)
        {
            return;
        }

        if (projectilePool == null)
        {
            return;
        }

        if (Input.GetKeyDown(startAutoKey))
        {
            StartAutoFire();
        }

        if (Input.GetKeyDown(stopAutoKey))
        {
            StopAutoFire();
        }

        if (Input.GetKeyDown(fireOnceKey))
        {
            FireOnceIfPossible();
        }
    }

    // ----------------------
    // Public API
    // ----------------------

    public void StartAutoFire()
    {
        if (isAutoFiring)
        {
            return;
        }

        if (ammoCount <= 0)
        {
            TryDestroySelfIfDepleted();
            return;
        }

        isAutoFiring = true;
        autoFireRoutine = StartCoroutine(AutoFireLoop());
    }

    public void StopAutoFire()
    {
        if (!isAutoFiring)
        {
            return;
        }

        if (autoFireRoutine != null)
        {
            StopCoroutine(autoFireRoutine);
            autoFireRoutine = null;
        }

        isAutoFiring = false;
        ClearAllReservations();
        TryDestroySelfIfDepleted();
    }

    public void FireOnceIfPossible()
    {
        if (board == null)
        {
            return;
        }

        if (projectilePool == null)
        {
            return;
        }

        if (ammoCount <= 0)
        {
            TryDestroySelfIfDepleted();
            return;
        }

        EnsureReservationsAllocated();

        int tx;
        int ty;
        bool found = TryFindClosestBottomRowMatchIgnoringReservations(board, cannonColor, out tx, out ty);

        if (!found)
        {
            return;
        }

        ReserveBottomCell(tx);

        Vector3 startPos;

        if (muzzle != null)
        {
            startPos = muzzle.position;
        }
        else
        {
            startPos = transform.position;
        }

        Vector3 targetPoint = board.CellToWorld(tx, ty);
        targetPoint.z = targetPoint.z + zOffset;

        Projectile proj = projectilePool.Get();
        if (proj == null)
        {
            // Pool exhausted and not allowed to expand
            UnreserveBottomCell(tx);
            return;
        }

        ammoCount = ammoCount - 1;
        inFlightCount = inFlightCount + 1;

        int targetX = tx;
        int targetY = ty;

        proj.LaunchToPoint(startPos, targetPoint, projectileSpeed, () =>
        {
            OnProjectileArrive_Pooled(proj, targetX, targetY);
        });

        TryDestroySelfIfDepleted();
    }

    // ----------------------
    // Core Loop
    // ----------------------

    private IEnumerator AutoFireLoop()
    {
        EnsureReservationsAllocated();

        while (true)
        {
            if (ammoCount <= 0)
            {
                break;
            }

            int tx;
            int ty;
            bool found = TryFindClosestBottomRowMatchIgnoringReservations(board, cannonColor, out tx, out ty);

            if (found)
            {
                ReserveBottomCell(tx);

                Vector3 startPos;

                if (muzzle != null)
                {
                    startPos = muzzle.position;
                }
                else
                {
                    startPos = transform.position;
                }

                Vector3 targetPoint = board.CellToWorld(tx, ty);
                targetPoint.z = targetPoint.z + zOffset;

                Projectile proj = projectilePool.Get();
                if (proj != null)
                {
                    ammoCount = ammoCount - 1;
                    inFlightCount = inFlightCount + 1;

                    int targetX = tx;
                    int targetY = ty;

                    proj.LaunchToPoint(startPos, targetPoint, projectileSpeed, () =>
                    {
                        OnProjectileArrive_Pooled(proj, targetX, targetY);
                    });
                }
                else
                {
                    // Pool could not provide a projectile; free the reservation.
                    UnreserveBottomCell(tx);
                }
            }
            else
            {
                break;
            }

            if (shotInterval > 0.0f)
            {
                float t = 0.0f;

                while (t < shotInterval)
                {
                    t = t + Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                yield return null;
            }
        }

        isAutoFiring = false;
        autoFireRoutine = null;

        ClearAllReservations();
        TryDestroySelfIfDepleted();
        yield break;
    }

    // ----------------------
    // Arrival & Pool Return
    // ----------------------

    private void OnProjectileArrive_Pooled(Projectile proj, int targetX, int targetY)
    {
        UnreserveBottomCell(targetX);

        if (board != null)
        {
            if (board.InBounds(targetX, targetY))
            {
                Block current = board.grid[targetX, targetY];

                if (current != null)
                {
                    if (current.Color == cannonColor)
                    {
                        board.DestroyAt(targetX, targetY, collapseDuration);
                    }
                }
            }
        }

        inFlightCount = Mathf.Max(0, inFlightCount - 1);

        if (projectilePool != null)
        {
            projectilePool.Release(proj);
        }

        TryDestroySelfIfDepleted();
    }

    // ----------------------
    // Targeting: closest to muzzle
    // ----------------------

    private bool TryFindClosestBottomRowMatchIgnoringReservations(Board b, BlockColor color, out int x, out int y)
    {
        x = -1;
        y = -1;

        if (b == null)
        {
            return false;
        }

        int row = 0;

        if (row < 0 || row >= b.height)
        {
            return false;
        }

        EnsureReservationsAllocated();

        Vector3 muzzlePos;

        if (muzzle != null)
        {
            muzzlePos = muzzle.position;
        }
        else
        {
            muzzlePos = transform.position;
        }

        bool foundAny = false;
        float bestDistSqr = float.PositiveInfinity;
        int bestCol = -1;

        for (int col = 0; col < b.width; col++)
        {
            if (!b.InBounds(col, row))
            {
                continue;
            }

            if (IsBottomCellReserved(col))
            {
                continue;
            }

            Block cell = b.grid[col, row];

            if (cell == null)
            {
                continue;
            }

            if (cell.Color != color)
            {
                continue;
            }

            Vector3 center = b.CellToWorld(col, row);

            float dx = center.x - muzzlePos.x;
            float dy = center.y - muzzlePos.y;
            float distSqr = dx * dx + dy * dy;

            if (!foundAny)
            {
                foundAny = true;
                bestDistSqr = distSqr;
                bestCol = col;
            }
            else
            {
                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    bestCol = col;
                }
            }
        }

        if (foundAny)
        {
            x = bestCol;
            y = row;
            return true;
        }

        return false;
    }

    // ----------------------
    // Reservations
    // ----------------------

    private void EnsureReservationsAllocated()
    {
        if (board == null)
        {
            return;
        }

        if (reservedBottomTargets == null || reservedBottomTargets.Length != board.width)
        {
            reservedBottomTargets = new bool[board.width];
        }
    }

    private void ClearAllReservations()
    {
        if (reservedBottomTargets == null)
        {
            return;
        }

        for (int i = 0; i < reservedBottomTargets.Length; i++)
        {
            reservedBottomTargets[i] = false;
        }
    }

    private void ReserveBottomCell(int x)
    {
        EnsureReservationsAllocated();

        if (x >= 0 && x < reservedBottomTargets.Length)
        {
            reservedBottomTargets[x] = true;
        }
    }

    private void UnreserveBottomCell(int x)
    {
        EnsureReservationsAllocated();

        if (x >= 0 && x < reservedBottomTargets.Length)
        {
            reservedBottomTargets[x] = false;
        }
    }

    private bool IsBottomCellReserved(int x)
    {
        EnsureReservationsAllocated();

        if (x >= 0 && x < reservedBottomTargets.Length)
        {
            return reservedBottomTargets[x];
        }

        return false;
    }

    // ----------------------
    // Cannon lifetime
    // ----------------------

    private void TryDestroySelfIfDepleted()
    {
        if (!destroyOnAmmoDepleted)
        {
            return;
        }

        if (ammoCount > 0)
        {
            return;
        }

        if (isAutoFiring)
        {
            return;
        }

        if (inFlightCount > 0)
        {
            return;
        }

        Destroy(gameObject);
    }
}
