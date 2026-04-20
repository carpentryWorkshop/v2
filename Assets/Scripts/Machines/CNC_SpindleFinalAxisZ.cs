using UnityEngine;

public class CNC_SpindleFinalAxisZ : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 0.01f;
    [SerializeField] private KeyCode positiveKey = KeyCode.O;
    [SerializeField] private KeyCode negativeKey = KeyCode.P;

    [Header("Z Limits")]
    [SerializeField] private float minZ = -0.02f;
    [SerializeField] private float maxZ = -0.01602f;

    private void Update()
    {
        Vector3 localPos = transform.localPosition;
        float input = 0f;

        if (Input.GetKey(positiveKey)) input += 1f;
        if (Input.GetKey(negativeKey)) input -= 1f;

        localPos.z += input * speed * Time.deltaTime;
        localPos.z = Mathf.Clamp(localPos.z, minZ, maxZ);
        transform.localPosition = localPos;
    }
}
