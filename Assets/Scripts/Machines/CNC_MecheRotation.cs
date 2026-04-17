using UnityEngine;

public class CNC_MecheRotation : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private float rotationSpeedDegPerSec = 1500f;

    private void Update()
    {
        float step = rotationSpeedDegPerSec * Time.deltaTime;
        transform.Rotate(0f, 0f, step, Space.Self);
    }
}
