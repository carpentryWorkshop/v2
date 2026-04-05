using UnityEngine;

public class PlankInteractable : Interactable

{

    private Rigidbody rb;

    private bool isHeld = false;

    private Transform holdPoint;



    public float snapSpeed = 20f;



    void Awake()

    {

        rb = GetComponent<Rigidbody>();

        if (rb != null)

            rb.isKinematic = true;

    }



    public override void Interact() { }



    void Update()

    {

        if (isHeld && holdPoint != null)

        {

            // Instantly snap to holdpoint - no lag

            transform.position = holdPoint.position;

            transform.rotation = holdPoint.rotation;

        }

    }



    public void PickUp()

    {

        // Find holdpoint at pickup time

        holdPoint = GameObject.Find("HoldPoint").transform;



        isHeld = true;

        if (rb != null)

        {

            rb.isKinematic = true;

            rb.useGravity = false;

        }

        Debug.Log("Plank picked up!");

    }



    public void PutDown()

    {

        isHeld = false;

        holdPoint = null;

        if (rb != null)

        {

            rb.isKinematic = false;

            rb.useGravity = true;

        }

        Debug.Log("Plank put down!");

    }

}