﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAgents;


public class DogAgent : Agent {

    [Header("Target To Walk Towards")] 

    public Transform target;
    public Transform spawnArea;
    public Bounds spawnAreaBounds;
    public bool respawnTargetWhenTouched;
    public float targetSpawnRadius;


    [Header("Body Parts")] 
    public Transform body;
    public Transform leg0_upper;
    public Transform leg1_upper;
    public Transform leg2_upper;
    public Transform leg3_upper;
    public Transform leg0_lower;
    public Transform leg1_lower;
    public Transform leg2_lower;
    public Transform leg3_lower;


    [Header("Orientation")] 
	public Vector3 dirToTarget;
	public Vector3 dirToTargetNormalized;
	public Vector3 bodyVelNormalized;
	public float movingTowardsDot;


    [Header("Reward Functions To Use")] 
    public bool rewardMovingTowardsTarget; //agent should move towards target
    public bool rewardFacingTarget; //agent should face the target
    public bool rewardUseTimePenalty; //hurry up


    List<Rigidbody> allRBs = new List<Rigidbody>();
    bool isNewDecisionStep;
    int currentDecisionStep;

    public float closestDistanceToTargetSoFarSqrMag;


    public float energyPenalty;
    public float energyReward;
    public float moveTowardsReward;
    public float facingReward;
    public float hurryUpReward;
    public float reachedTargetReward;

    public float totalReward;
    public float agentReward;
    public float maxTurnSpeed;
    public ForceMode turningForceMode;
    public float turnOffset;

    [Header("Bone Info")] 
    [Space(10)] 

    public bool waiting;
    public bool fetching;
    public bool returning;

    public bool shouldGoGetBone;
    public bool runningToBone;
    public bool shouldIdle;
    public bool idling;
    public bool shouldPickUpBone;
    public bool shouldReturnTheBone;
    public bool hasBone;
    public bool returningBone;
    public bool needToGoGetBone;
    public bool needToReturnBone;
    public ThrowBone boneController;
    public Transform mouthPosition;


    public bool trainingMode;
    public bool gameMode;

    JointDriveController jdController;

	public List<AudioClip> barkSounds = new List <AudioClip>();
	public AudioSource audioSourceSFX;


    void Awake()
    // void InitializeAgent()
    {
        AllStatesFalse();
        audioSourceSFX = body.gameObject.AddComponent<AudioSource>();
        audioSourceSFX.spatialBlend = .75f;
        audioSourceSFX.minDistance = .7f;
        audioSourceSFX.maxDistance = 5;
        boneController = FindObjectOfType<ThrowBone>();
        if(gameMode)
        {
            target = boneController.returnPoint;
        }
        // Get the ground's bounds
        spawnAreaBounds = spawnArea.GetComponent<Collider>().bounds;
        closestDistanceToTargetSoFarSqrMag = 10000;
        jdController = GetComponent<JointDriveController>();

        // jdController.bodyPartsDictList.Clear();
        // speedUI = FindObjectOfType<SpeedUI>();
        // jersey.SetActive(false);
        //Setup each body part
        jdController.SetupBodyPart(body);
        jdController.SetupBodyPart(leg0_upper);
        jdController.SetupBodyPart(leg0_lower);
        jdController.SetupBodyPart(leg1_upper);
        jdController.SetupBodyPart(leg1_lower);
        jdController.SetupBodyPart(leg2_upper);
        jdController.SetupBodyPart(leg2_lower);
        jdController.SetupBodyPart(leg3_upper);
        jdController.SetupBodyPart(leg3_lower);



        allRBs.AddRange(gameObject.GetComponentsInChildren<Rigidbody>());
        currentDecisionStep = 1;
        StartCoroutine(BarkBark());
    }



    /// <summary>
    /// Use the ground's bounds to pick a random spawn position.
    /// </summary>
    public void SpawnBone()
    {
        Vector3 randomSpawnPos = Vector3.zero;
        float randomPosX = Random.Range(-spawnAreaBounds.extents.x, spawnAreaBounds.extents.x);
        float randomPosZ = Random.Range(-spawnAreaBounds.extents.z, spawnAreaBounds.extents.z);
        target.position = spawnArea.transform.position + new Vector3(randomPosX, 1f, randomPosZ);
    }

	/// <summary>
    /// Agent touched the target
    /// </summary>
	public void TouchedTarget()
	{
		AddReward(1); //good boy
        if(respawnTargetWhenTouched)
        {
		     SpawnBone();
        }
		Done();
	}

    public void PickUpBone()
    {
        // if(Application.isEditor && boneController)
        // {
            // print("PickUpBone()");
            hasBone = true;
            boneController.boneCol.enabled = false;
            boneController.boneRB.isKinematic = true;
            boneController.bone.position = mouthPosition.position;
            boneController.bone.rotation = mouthPosition.rotation;
            boneController.bone.SetParent(mouthPosition);


            target = boneController.returnPoint;
            returningBone = true;
        // }

        if(brain.brainType == BrainType.External) //if it's an external brain then we are training.
        {
            TouchedTarget();
        }
    }

    public void DropBone()
    {
        if(boneController)
        {
            boneController.boneRB.isKinematic = false;
            boneController.bone.parent = null;
            hasBone = false;
            boneController.boneCol.enabled = true;
            shouldIdle = true;
        }
    }

    /// <summary>
    /// Add relevant information on each body part to observations.
    /// </summary>
    // public void CollectObservationBodyPart(BodyPart bp)
    public void CollectObservationBodyPart(BodyPart bp)
    {
        var rb = bp.rb;
        AddVectorObs(bp.groundContact.touchingGround ? 1 : 0); // Is this bp touching the ground
        if(bp.rb.transform != body)
        {

            AddVectorObs(bp.currentXNormalizedRot);
            AddVectorObs(bp.currentYNormalizedRot);
            AddVectorObs(bp.currentZNormalizedRot);
            AddVectorObs(bp.currentStrength/jdController.maxJointForceLimit);
        }
    }

    public override void CollectObservations()
    {
        AddVectorObs(dirToTarget.normalized);
        AddVectorObs(body.localPosition);
        AddVectorObs(jdController.bodyPartsDict[body].rb.velocity);
        AddVectorObs(jdController.bodyPartsDict[body].rb.angularVelocity);
        AddVectorObs(body.forward); //the capsule is rotated so this is local forward
        AddVectorObs(body.up); //the capsule is rotated so this is local forward
        foreach (var bodyPart in jdController.bodyPartsDict.Values)
        {
            CollectObservationBodyPart(bodyPart);
        }
    }




    //We only need to change the joint settings based on decision freq.
    public void IncrementDecisionTimer()
    {
        if(currentDecisionStep == this.agentParameters.numberOfActionsBetweenDecisions || this.agentParameters.numberOfActionsBetweenDecisions == 1)
        {
            currentDecisionStep = 1;
            isNewDecisionStep = true;
        }
        else
        {
            currentDecisionStep ++;
            isNewDecisionStep = false;
        }
    }

    void RotateBody(float act)
    {
        //body rotation
        var targetRot = Quaternion.LookRotation(dirToTarget); //dir to rotate
        float speed = Mathf.Lerp(0, maxTurnSpeed, Mathf.Clamp(act, 0, 1));
        var newRot = Quaternion.Lerp(jdController.bodyPartsDict[body].rb.rotation, targetRot, speed * Time.deltaTime); //lerp it so it's not dramatic
        Vector3 rotDir = dirToTarget; 
        rotDir.y = 0;
        // jdController.bodyPartsDict[body].rb.MoveRotation(newRot);
        jdController.bodyPartsDict[body].rb.AddForceAtPosition(rotDir.normalized * speed * Time.deltaTime, body.forward * turnOffset, turningForceMode); //tug on the front
        jdController.bodyPartsDict[body].rb.AddForceAtPosition(-rotDir.normalized * speed * Time.deltaTime, -body.forward * turnOffset, turningForceMode); //tug on the back
    }


    void AllStatesFalse()
    {
        shouldGoGetBone = false;
        runningToBone = false;
        shouldPickUpBone = false;
        hasBone = false;
        shouldReturnTheBone = false;
        returningBone = false;
        shouldIdle = false;
        idling = false;
    }

    public IEnumerator BarkBark()
    {       
        while(true)
        {
            if(!returningBone)
            {
                audioSourceSFX.PlayOneShot(barkSounds[Random.Range( 0, barkSounds.Count)], 1);
            }
            yield return new WaitForSeconds(Random.Range(1, 10));
        }
    }
    public IEnumerator GoGetBone()
    {   
        //GO GET THE STICK
        target = boneController.bone;
        runningToBone = true;

        //WHEN WE'RE IN RANGE
        while(dirToTarget.sqrMagnitude > 1f)
        {
            yield return null;
        }
        PickUpBone();
        runningToBone = false;

        //RETURN THE STICK
        target = boneController.returnPoint;
        returningBone = true;
        yield return null; //wait a step

        //WHEN WE'RE IN RANGE
        while(dirToTarget.sqrMagnitude > 1f)
        {
            yield return null;
        }
        DropBone();
        returningBone = false;
        boneController.canThrowBone = true;
    }


	public override void AgentAction(float[] vectorAction, string textAction)
    {

        foreach (var bp in jdController.bodyPartsDict.Values)
        {
            if(!IsDone() && bp.targetContact.touchingTarget)
            {
                TouchedTarget();
            }
        }

        dirToTarget = target.position - jdController.bodyPartsDict[body].rb.position;
        dirToTargetNormalized = dirToTarget.normalized;
        bodyVelNormalized = jdController.bodyPartsDict[body].rb.velocity.normalized;

        // if(shouldGoGetBone && !runningToBone)
        // {
        //     idling = false;
        //     target = boneController.bone;
        //     runningToBone = true;
        // }

        // // if(shouldPickUpBone && !hasBone)
        // if(shouldPickUpBone && !hasBone)
        // {
        //     PickUpBone();
        // }

        // if(shouldReturnTheBone && !returningBone)
        // {
        //     target = boneController.returnPoint;
        //     returningBone = true;
        // }

        // if(returningBone && dirToTarget.magnitude < 1)
        // {
        //     DropBone();
        // }
        // if(shouldIdle && !idling)
        // {
        //     //idle logic. may need diff brain
        //     StartIdling();
        // }

        agentReward = GetCumulativeReward();

        float dirSqr = dirToTarget.sqrMagnitude;
        if(dirSqr < closestDistanceToTargetSoFarSqrMag)
        {
            // AddReward(0.01f * Mathf.Clamp(closestDistanceToTargetSoFarSqrMag - dirSqr, 0, 1));
            closestDistanceToTargetSoFarSqrMag = dirSqr;

        }

        if(isNewDecisionStep)
        {
            var bpDict = jdController.bodyPartsDict;
            int i = -1;

            bpDict[leg0_upper].SetJointTargetRotation(vectorAction[0], vectorAction[1], 0);
            bpDict[leg1_upper].SetJointTargetRotation(vectorAction[2], vectorAction[3], 0);
            bpDict[leg2_upper].SetJointTargetRotation(vectorAction[4], vectorAction[5], 0);
            bpDict[leg3_upper].SetJointTargetRotation(vectorAction[6], vectorAction[7], 0);
            bpDict[leg0_lower].SetJointTargetRotation(vectorAction[8], 0, 0);
            bpDict[leg1_lower].SetJointTargetRotation(vectorAction[9], 0, 0);
            bpDict[leg2_lower].SetJointTargetRotation(vectorAction[19], 0, 0);
            bpDict[leg3_lower].SetJointTargetRotation(vectorAction[20], 0, 0);

            //update joint drive settings
            bpDict[leg0_upper].SetJointStrength(vectorAction[8]);
            bpDict[leg1_upper].SetJointStrength(vectorAction[9]);
            bpDict[leg2_upper].SetJointStrength(vectorAction[10]);
            bpDict[leg3_upper].SetJointStrength(vectorAction[11]);
            bpDict[leg0_lower].SetJointStrength(vectorAction[17]);
            bpDict[leg1_lower].SetJointStrength(vectorAction[18]);
            bpDict[leg2_lower].SetJointStrength(vectorAction[14]);
            bpDict[leg3_lower].SetJointStrength(vectorAction[15]);

            RotateBody(vectorAction[16]);

        }
        var bodyRotationPenalty = -.001f * vectorAction[12]; //rotation strength
        AddReward(bodyRotationPenalty);

        // Set reward for this step according to mixture of the following elements.
        if(rewardMovingTowardsTarget){RewardFunctionMovingTowards();}
        // if(rewardFacingTarget){RewardFunctionFacingTarget();}
        if(rewardUseTimePenalty){RewardFunctionTimePenalty();}
        IncrementDecisionTimer();
    }
	
    //Reward moving towards target & Penalize moving away from target.
    void RewardFunctionMovingTowards()
    {
		movingTowardsDot = Vector3.Dot(jdController.bodyPartsDict[body].rb.velocity, dirToTarget.normalized); 
        moveTowardsReward += 0.01f * movingTowardsDot;
        totalReward += moveTowardsReward;
        AddReward(0.01f * movingTowardsDot);
    }

    //Time penalty - HURRY UP
    void RewardFunctionTimePenalty()
    {
        //0.001f chosen by experimentation. If this penalty is too high it will kill itself :(
        hurryUpReward += -.001f;
        totalReward += hurryUpReward;
        AddReward(- 0.001f); 
        // AddReward(- 0.001f); 
    }
    

	/// <summary>
    /// Loop over body parts and reset them to initial conditions.
    /// </summary>
    public override void AgentReset()
    {
        foreach (var bodyPart in jdController.bodyPartsDict.Values)
        {
            bodyPart.Reset();
        }
        currentDecisionStep = 1;
        isNewDecisionStep = true;
        closestDistanceToTargetSoFarSqrMag = 10000;
        energyReward = 0;
        moveTowardsReward = 0;
        facingReward = 0;
        hurryUpReward = 0;
        totalReward = 0;
        reachedTargetReward = 0;
        energyPenalty = 0;
    }
}