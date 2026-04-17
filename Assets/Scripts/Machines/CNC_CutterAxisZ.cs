using UnityEngine;

public class CNC_CutterAxisZ : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 2f;
    [SerializeField] private KeyCode positiveKey = KeyCode.I;
    [SerializeField] private KeyCode negativeKey = KeyCode.K;

    [Header("Z Limits")]
    [SerializeField] private float minZ = -7f;
    [SerializeField] private float maxZ = 5f;

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
