using System;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 12.0f;

    private Vector3 targetWorld;
    private bool isActive;
    private Action onArrive;

    public void LaunchToPoint(Vector3 startWorld, Vector3 targetWorld, float projectileSpeed, Action onArrive)
    {
        transform.position = startWorld;
        this.targetWorld = targetWorld;
        speed = projectileSpeed;
        this.onArrive = onArrive;
        isActive = true;
    }

    private void Update()
    {
        if (!isActive)
        {
            return;
        }

        Vector3 toTarget = targetWorld - transform.position;
        float distance = toTarget.magnitude;

        if (distance <= 0.001f)
        {
            transform.position = targetWorld;
            isActive = false;

            if (onArrive != null)
            {
                onArrive.Invoke();
            }

            return;
        }

        Vector3 step = toTarget.normalized * speed * Time.deltaTime;

        if (step.magnitude >= distance)
        {
            transform.position = targetWorld;
            isActive = false;

            if (onArrive != null)
            {
                onArrive.Invoke();
            }

            return;
        }

        transform.position = transform.position + step;
    }
}
