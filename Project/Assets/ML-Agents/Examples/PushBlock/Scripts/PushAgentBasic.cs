//Put this script on your blue cube.

using System.Collections;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Sensors.Reflection;

public class PushAgentBasic : Agent
{
    /// <summary>
    /// The ground. The bounds are used to spawn the elements.
    /// </summary>
    public GameObject ground;

    public GameObject area;

    /// <summary>
    /// The area bounds.
    /// </summary>
    [HideInInspector]
    public Bounds areaBounds;

    PushBlockSettings m_PushBlockSettings;


	/// <summary>
	/// The hook to reach the block.
	/// </summary>
	public GameObject crane_hook;
	public GameObject child_mast;
	
    /// <summary>
    /// The goal to push the block to.
    /// </summary>
    public GameObject goal;

    /// <summary>
    /// The block to be pushed to the goal.
    /// </summary>
    public GameObject block;

	/// <summary>
	/// Animator
	/// </summary>
	public Animator animator;
	public AnimatorControllerParameter animParam;
	
	/// <summary>
	/// Observerble attributes로 하면 좋을듯? 위치 벡터랑 같이
	/// </summary>
	/// 
	public	float prevBest;
	public	float prevBestAngle;
	
	public float	GetAngle (Vector3 vStart, Vector3 vEnd){
		Vector3 v = vEnd - vStart;
		return Mathf.Atan2(v.z, v.x) ; // * Mathf.Rad2Deg;
	}
	
	[Observable]
	public float AngleHookBlock
	{
		get { return ( Mathf.Cos(GetAngle( block.transform.position, crane_hook.transform.position ) )); }
	}
	
	public float rotateYaw;
	[Observable]
	public float roteteYaw_nor
	{
		get { return ( rotateYaw % 360 ) / 360 ; }
	}
	
	public float dolly;
	[Observable]
	public float dolly_nor
	{
		get { return ( dolly / 100 ); }
	}
	
	public float hook;
	[Observable]
	float hook_nor
	{
		get { return ( hook	/ 100 ); }
	}	
	[Observable] // (numStackedObservations: 2)
	public	float distanceToBlock
	{
		get { return Vector3.Distance(crane_hook.transform.position, block.transform.position); }
	}
	
	//[Observable]
	//public Vector3 HookPosition
	//{
	//	get { return crane_hook.transform.position;} // .normialized
	//}
	//[Observable]
	//public Vector3 BlockPosition
	//{
	//	get { return block.transform.position;} // .normalized
	//}	
	[Observable]
	public Vector3 Destination
	{
		get { return (crane_hook.transform.position.normalized - block.transform.position.normalized); }
	}

    /// <summary>
    /// Detects when the block touches the goal.
    /// </summary>
    [HideInInspector]
    public GoalDetect goalDetect;

    public bool useVectorObs;

    Rigidbody m_BlockRb;  //cached on initialization
    Rigidbody m_AgentRb;  //cached on initialization
    Material m_GroundMaterial; //cached on Awake()

    /// <summary>
    /// We will be changing the ground material based on success/failue
    /// </summary>
    Renderer m_GroundRenderer;

    EnvironmentParameters m_ResetParams;

    void Awake()
    {
        m_PushBlockSettings = FindObjectOfType<PushBlockSettings>();
    }

    public override void Initialize()
    {
        goalDetect = block.GetComponent<GoalDetect>();
        goalDetect.agent = this;

	    prevBest = distanceToBlock;
	    prevBestAngle = AngleHookBlock; 
	    
        // Cache the agent rigidbody
	    m_AgentRb = crane_hook.GetComponent<Rigidbody>();
        // Cache the block rigidbody
        m_BlockRb = block.GetComponent<Rigidbody>();
        // Get the ground's bounds
        areaBounds = ground.GetComponent<Collider>().bounds;
        // Get the ground renderer so we can change the material when a goal is scored
        m_GroundRenderer = ground.GetComponent<Renderer>();
        // Starting material
        m_GroundMaterial = m_GroundRenderer.material;

        m_ResetParams = Academy.Instance.EnvironmentParameters;
		
	    // agent localization
	    animator = GetComponent<Animator>();
	    
	    rotateYaw = Random.Range( -90, 90 );
		dolly = Random.Range( 0, 100.0f);
	    hook = Random.Range( 0, 100.0f);

	    animator.SetFloat( "Rotate_YAW",  rotateYaw * 2 % 360  );
		animator.SetFloat( "dolly", dolly );
	    animator.SetFloat( "hook", hook );
		
        SetResetParameters();
    }

    /// <summary>
    /// Use the ground's bounds to pick a random spawn position.
    /// </summary>
    public Vector3 GetRandomSpawnPos()
    {
        var foundNewSpawnLocation = false;
        var randomSpawnPos = Vector3.zero;
	    while (foundNewSpawnLocation == false)
	    {
            var randomPosX = Random.Range(-areaBounds.extents.x * m_PushBlockSettings.spawnAreaMarginMultiplier,
                areaBounds.extents.x * m_PushBlockSettings.spawnAreaMarginMultiplier);

            var randomPosZ = Random.Range(-areaBounds.extents.z * m_PushBlockSettings.spawnAreaMarginMultiplier,
                areaBounds.extents.z * m_PushBlockSettings.spawnAreaMarginMultiplier);
            randomSpawnPos = ground.transform.position + new Vector3(randomPosX, 1f, randomPosZ);
	        if (Physics.CheckBox(randomSpawnPos, new Vector3(3.5f, 0.01f, 3.5f)) == false)
            {
                foundNewSpawnLocation = true;
            }
        }
        return randomSpawnPos;
    }

    /// <summary>
    /// Called when the agent moves the block into the goal.
    /// </summary>
    public void ScoredAGoal()
    {
        // We use a reward of 5.
        AddReward(5f);

        // By marking an agent as done AgentReset() will be called automatically.
        EndEpisode();

        // Swap ground material for a bit to indicate we scored.
        StartCoroutine(GoalScoredSwapGroundMaterial(m_PushBlockSettings.goalScoredMaterial, 0.5f));
    }

    /// <summary>
    /// Swap ground material, wait time seconds, then swap back to the regular material.
    /// </summary>
    IEnumerator GoalScoredSwapGroundMaterial(Material mat, float time)
    {
        m_GroundRenderer.material = mat;
        yield return new WaitForSeconds(time); // Wait for 2 sec
        m_GroundRenderer.material = m_GroundMaterial;
    }

	/// <summary> 속도 조절~~
	/// Moves the agent according to the selected action.
	/// 속도 조절~~
	/// </summary> 속도 조절~~
    public void MoveAgent(ActionSegment<int> act)
	{
		float distanceToBlock = Vector3.Distance(crane_hook.transform.position, block.transform.position);
    	
	    var	Vyaw = 0f;
	    var	Vdolly = 0f;
	    var	Vhook = 0f;
	    
        var action = act[0];

        switch (action)
        {
            case 1:
	            Vyaw = 1f; ///회전 속도
                break;
            case 2:
	            Vyaw = -1f;
                break;
            case 3:
	            Vdolly = 0.5f; ///앞.뒤 속도
                break;
            case 4:
	            Vdolly = -0.5f;
                break;
            case 5:
	            Vhook = -0.5f; ///위.아래 속도, 올라감
                break;
            case 6:
	            Vhook = 0.5f; ///내려감
                break;
        }
	
	    rotateYaw	+= Vyaw;
	    dolly	+= Vdolly;
	    hook	+= Vhook;
	    
		animator.SetFloat( "Rotate_YAW", rotateYaw % 360  );
		animator.SetFloat( "dolly", dolly );
		animator.SetFloat( "hook", hook );
	    
		if (distanceToBlock < 2.5f)
		{
			block.transform.position = crane_hook.transform.position + new Vector3(0, -0.5f, 0);
		}
    }

	/// <summary>
	/// 벡터관측
	/// </summary>
	//public override void CollectObservations(VectorSensor sensor)
	//{
	//sensor.AddObservation(crane_hook.transform.position);
	//sensor.AddObservation(crane_hook.transform.position - block.transform.position);
	//sensor.AddObservation(block.transform.position);
	//sensor.AddObservation(hook);
	//sensor.AddObservation(dolly);
	//sensor.AddObservation(rotateYaw);
		//float distanceToBlock = Vector3.Distance(crane_hook.transform.position, block.transform.position);
		//sensor.AddObservation(distanceToBlock);
	//sensor.AddObservation(Vector3.Distance(crane_hook.transform.position, block.transform.position));
		
		
	//}
	
    /// <summary>
    /// Called every step of the engine. Here the agent takes an action.
    /// </summary>
    public override void OnActionReceived(ActionBuffers actionBuffers)

    {
        // Move the agent using the action.
        MoveAgent(actionBuffers.DiscreteActions);
		
	    float distanceToBlock = Vector3.Distance(crane_hook.transform.position, block.transform.position);
	    float diff = prevBest-distanceToBlock;
	    float diffAngle = AngleHookBlock - prevBestAngle ;
		
	    if ( AngleHookBlock < prevBestAngle	)
	    { // 멀어질 때 페널티
	    	AddReward( 0.01f * ( diffAngle - 0.001f ) );
	    }
	    else
	    {	// 가까워 질 때 보상
	    	AddReward ( 0.02f * ( diffAngle + 0.001f ) );
	    	prevBestAngle = AngleHookBlock;
	    }
	    
	    
	    if ( distanceToBlock > prevBest	)
	    { // 멀어질 때 페널티
	    	AddReward( 0.0003f * ( diff - 0.01f ) );
	    }
	    else
	    {	// 가까워 질 때 보상
	    	AddReward ( 0.0005f * ( diff + 0.01f ) );
	    	prevBest = distanceToBlock;
	    }
	    //{
	    //  if (distanceToBlock <= 1.5f)
	    //   { AddReward ( 1.5f ); }
	    //  else if (distanceToBlock < 4.5f)
	    //   { AddReward ( 0.02f ); }
	    //  else if (distanceToBlock < 13.5f)
	    //   { AddReward ( 0.002f ); }
	    //   else
	    //   { AddReward ( -0.001f );}
	    
			//AddReward( 2.25f / ( distanceToBlock * distanceToBlock )  );
	    //}
	
        // Penalty given each step to encourage agent to finish task quickly.
	    AddReward( -1f / MaxStep);	
	    
	    // 훅이 지면 아래로 내려가면 페널티.
	    if (crane_hook.transform.position.y	< 0.0f)
	    {
		    AddReward ( -1f);    	
	    } 
		
	    Debug.Log("diff : " + diff	);
	    Debug.Log("diffAngle : " + diffAngle );
	    
	    //Debug.Log("Distance : " + distanceToBlock);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut[0] = 0;
	    if (Input.GetKey(KeyCode.A))
        {
	        discreteActionsOut[0] = 1;
        }
        else if (Input.GetKey(KeyCode.W))
        {
	        discreteActionsOut[0] = 3;
        }
        else if (Input.GetKey(KeyCode.D))
        {
	        discreteActionsOut[0] = 2;
        }
        else if (Input.GetKey(KeyCode.S))
        {
	        discreteActionsOut[0] = 4;
        }
        else if (Input.GetKey(KeyCode.C))
        {
	        discreteActionsOut[0] = 6;
        }
        else if (Input.GetKey(KeyCode.E))
        {
	        discreteActionsOut[0] = 5;
        }
    }

    /// <summary>
    /// Resets the block position and velocities.
    /// </summary>
    void ResetBlock()
    {
        // Get a random position for the block.
        block.transform.position = GetRandomSpawnPos();

        // Reset block velocity back to zero.
        m_BlockRb.velocity = Vector3.zero;

        // Reset block angularVelocity back to zero.
        m_BlockRb.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// In the editor, if "Reset On Done" is checked then AgentReset() will be
    /// called automatically anytime we mark done = true in an agent script.
    /// </summary>
    public override void OnEpisodeBegin()
    {
        var rotation = Random.Range(0, 4);
        var rotationAngle = rotation * 90f;
        area.transform.Rotate(new Vector3(0f, rotationAngle, 0f));

        ResetBlock();
	    animator.SetFloat( "Rotate_YAW", Random.Range(-90, 90) * 2 );
	    animator.SetFloat( "dolly", Random.Range(0f, 100.0f) );
	    animator.SetFloat( "hook", Random.Range(0f, 100.0f) );
	    ///transform.position = GetRandomSpawnPos();
	    ///m_AgentRb.velocity = Vector3.zero;
	    ///m_AgentRb.angularVelocity = Vector3.zero; 

        SetResetParameters();
    }

    public void SetGroundMaterialFriction()
    {
        var groundCollider = ground.GetComponent<Collider>();

        groundCollider.material.dynamicFriction = m_ResetParams.GetWithDefault("dynamic_friction", 0);
        groundCollider.material.staticFriction = m_ResetParams.GetWithDefault("static_friction", 0);
    }

    public void SetBlockProperties()
    {
        var scale = m_ResetParams.GetWithDefault("block_scale", 2);
        //Set the scale of the block
        m_BlockRb.transform.localScale = new Vector3(scale, 0.75f, scale);

        // Set the drag of the block
        m_BlockRb.drag = m_ResetParams.GetWithDefault("block_drag", 0.5f);
    }

    void SetResetParameters()
    {
        SetGroundMaterialFriction();
        SetBlockProperties();
    }
}
