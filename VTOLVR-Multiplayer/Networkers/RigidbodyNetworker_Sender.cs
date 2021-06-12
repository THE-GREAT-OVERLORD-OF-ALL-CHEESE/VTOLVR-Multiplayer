﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class RigidbodyNetworker_Sender : MonoBehaviour
{
    public ulong networkUID;
    private Rigidbody rb;
    private Message_RigidbodyUpdate lastMessage;
    public Vector3 originOffset;
    private Vector3D globalLastPosition;
    private Vector3 localLastPosition;
    private Vector3 lastVelocity;
    private Vector3 lastUp;
    private Vector3 lastForward;
    private Quaternion lastRotation;
    private Vector3 lastAngularVelocity;
    private float threshold = 0.5f;
    private float angleThreshold = 1f;

    private ulong updateNumber;
    private float tick;
    public float tickRate = 10;

    public int first = 0;
    public bool player = false;

    public Vector3 spawnPosf;
    public Quaternion spawnRotf;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        lastMessage = new Message_RigidbodyUpdate(new Vector3D(), new Vector3D(), new Vector3D(), Quaternion.identity, 0, networkUID);
        tick = 0;

        tick += UnityEngine.Random.Range(0.0f, 1.0f / tickRate);
    }
  
        private void FixedUpdate()
    {
        globalLastPosition += new Vector3D(lastVelocity * Time.fixedDeltaTime);
        localLastPosition = VTMapManager.GlobalToWorldPoint(globalLastPosition);
        Quaternion quatVel = Quaternion.Euler(lastAngularVelocity * Time.fixedDeltaTime);
        lastRotation *= quatVel;

        lastUp = lastRotation * Vector3.up;
        lastForward = lastRotation * Vector3.forward;
        tick += Time.fixedDeltaTime;
        if (tick > 1.0f / tickRate)
        {
            tick = 0.0f;
            lastUp = transform.up;
            lastForward = transform.forward;

            globalLastPosition = VTMapManager.WorldToGlobalPoint(transform.TransformPoint(originOffset));
            lastVelocity = rb.velocity;

            lastRotation = transform.rotation;
            lastAngularVelocity = rb.angularVelocity * Mathf.Rad2Deg;

            lastMessage.position = VTMapManager.WorldToGlobalPoint(transform.TransformPoint(originOffset));
            lastMessage.rotation = transform.rotation;

            lastMessage.velocity = new Vector3D(rb.velocity);
            lastMessage.angularVelocity = new Vector3D(rb.angularVelocity * Mathf.Rad2Deg);
            lastMessage.networkUID = networkUID;
            lastMessage.sequenceNumber = PlayerManager.timeinGame;
            if (Networker.isHost)
            {
                Networker.addToUnreliableSendBuffer(lastMessage);
            }
            else
                NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, lastMessage, Steamworks.EP2PSend.k_EP2PSendUnreliable);
        }
    }

    public void SetSpawn(Vector3 spawnPos, Quaternion spawnRot)
    {
        Debug.Log($"starting spawn repositioner");
        spawnPosf = spawnPos;
        spawnRotf = spawnRot;
        StartCoroutine(SetSpawnEnumerator(spawnPos, spawnRot));
    }

    private IEnumerator SetSpawnEnumerator(Vector3 spawnPos, Quaternion spawnRot)
    {
        rb.interpolation = RigidbodyInterpolation.None;
        rb.isKinematic = true;
        rb.velocity = new Vector3(0, 0, 0); rb.Sleep();
        rb.position = spawnPos;
        rb.transform.position = spawnPos;
        rb.transform.rotation = spawnRot;
        rb.Sleep();

        player = true;
        Physics.SyncTransforms();
        Debug.Log($"Our position is now {rb.position}");

        yield return new WaitForSeconds(0.5f);
        rb.detectCollisions = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;


    }
}
