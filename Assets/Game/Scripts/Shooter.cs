using System.Collections;
using UnityEngine;

public class Shooter : MonoBehaviour
{
    [Header("References")]
    public Board board;
    public Projectile projectilePrefab;
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
        if (board == null || projectilePrefab == null)
        {
            return;
        }

        // Hotkeys (optional)
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

    /// <summary>
    /// Starts continuous firing: shoots on a cadence (shotInterval) without waiting for impacts.
    /// Stops automatically when no bottom-row targets remain or ammo is depleted.
    /// </summary>
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

    /// <summary>
    /// Stops continuous firing. Existing projectiles keep flying.
    /// </summary>
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
        TryDestroySelfIfDepleted(); // in case we stopped with no ammo left and no shots in flight
    }

    /// <summary>
    /// Fires exactly one shot immediately if a bottom-row target exists and ammo > 0.
    /// Does not affect auto-fire state.
    /// </summary>
    public void FireOnceIfPossible()
    {
        if (board == null)
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
        bool found = TryFindNextBottomRowMatchIgnoringReservations(board, cannonColor, out tx, out ty);

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

        ammoCount = ammoCount - 1;
        inFlightCount = inFlightCount + 1;

        Projectile proj = Instantiate(projectilePrefab);
        proj.LaunchToPoint(startPos, targetPoint, projectileSpeed, () =>
        {
            OnProjectileArrive(tx, ty);
        });

        TryDestroySelfIfDepleted(); // may self-destruct later when inFlight hits 0
    }

    // ----------------------
    // Core Loops
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
            bool found = TryFindNextBottomRowMatchIgnoringReservations(board, cannonColor, out tx, out ty);

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
                targetPoint.z += zOffset;

                ammoCount--;
                inFlightCount++;

                Projectile proj = Instantiate(projectilePrefab);
                proj.LaunchToPoint(startPos, targetPoint, projectileSpeed, () =>
                {
                    OnProjectileArrive(tx, ty);
                });
            }
            else
            {
                // No current target on bottom row → stop auto firing.
                break;
            }

            // Pace control: do not wait for impact; just wait the shotInterval.
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
                // Safety yield
                yield return null;
            }
        }

        isAutoFiring = false;
        autoFireRoutine = null;

        // When we stop, clear reservations so a new session can re-target freely.
        ClearAllReservations();

        TryDestroySelfIfDepleted();
        yield break;
    }

    // ----------------------
    // Arrival & Destruction
    // ----------------------

    private void OnProjectileArrive(int targetX, int targetY)
    {
        // Free the reservation for that bottom cell index regardless of outcome.
        UnreserveBottomCell(targetX);

        // Gate: impact may happen after the board changed.
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

        // Allow the collapse animation to proceed visually; we do not block firing on it.
        // Nothing to do here other than decrement in-flight and possibly self-destruct.
        inFlightCount = Mathf.Max(0, inFlightCount - 1);

        TryDestroySelfIfDepleted();
    }

    // ----------------------
    // Targeting Helpers
    // ----------------------

    private bool TryFindNextBottomRowMatchIgnoringReservations(Board b, BlockColor color, out int x, out int y)
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

            if (cell.Color == color)
            {
                x = col;
                y = row;
                return true;
            }
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

        // If auto-firing or shots are still in flight, wait until they finish.
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
