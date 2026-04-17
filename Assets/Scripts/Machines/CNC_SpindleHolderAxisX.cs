using UnityEngine;

public class CNC_SpindleHolderAxisX : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 0.02f;
    [SerializeField] private KeyCode positiveKey = KeyCode.L;
    [SerializeField] private KeyCode negativeKey = KeyCode.J;

    [Header("X Limits")]
    [SerializeField] private float minX = 0.0044f;
    [SerializeField] private float maxX = 0.06139f;

    private void Update()
    {
        Vector3 localPos = transform.localPosition;
        float input = 0f;

        if (Input.GetKey(positiveKey)) input += 1f;
        if (Input.GetKey(negativeKey)) input -= 1f;

        localPos.x += input * speed * Time.deltaTime;
        localPos.x = Mathf.Clamp(localPos.x, minX, maxX);
        transform.localPosition = localPos;
    }
}
