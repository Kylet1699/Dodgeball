using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;

public enum DodgeballTeam
{
    Blue = 0,
    Purple = 1
}

public class AgentDodge : Agent
{
    // Note that that the detectable tags are different for the blue and purple teams. The order is
    // * ball
    // * own goal
    // * opposing goal
    // * wall
    // * own teammate
    // * opposing player

    [HideInInspector]
    public DodgeballTeam team;
    float m_KickPower;
    // The coefficient for the reward for colliding with a ball. Set using curriculum.
    float m_BallTouch;
    //public Position position;

    const float k_Power = 2000f;
    float m_Existential;
    float m_LateralSpeed;
    float m_ForwardSpeed;


    [HideInInspector]
    public Rigidbody agentRb;
    DodgeSettings m_DodgeSettings;
    BehaviorParameters m_BehaviorParameters;
    public Vector3 initialPos;
    public float rotSign;

    EnvironmentParameters m_ResetParams;

    public GameObject grabbedObject;
    public float grabbedObjectSize;
    public int inventory;

    public GameObject projectile;
    public float launchVelocity = 10f;
    public DodgeEnvController envController;

    public override void Initialize()
    {
        DodgeEnvController envController = GetComponentInParent<DodgeEnvController>();
        if (envController != null)
        {
            m_Existential = 1f / envController.MaxEnvironmentSteps;
        }
        else
        {
            m_Existential = 1f / MaxStep;
        }

        m_BehaviorParameters = gameObject.GetComponent<BehaviorParameters>();
        if (m_BehaviorParameters.TeamId == (int)DodgeballTeam.Blue)
        {
            team = DodgeballTeam.Blue;
            initialPos = new Vector3(transform.position.x - 5f, .5f, transform.position.z);
            rotSign = 1f;
        }
        else
        {
            team = DodgeballTeam.Purple;
            initialPos = new Vector3(transform.position.x + 5f, .5f, transform.position.z);
            rotSign = -1f;
        }
        m_LateralSpeed = 0.2f;
        m_ForwardSpeed = 0.2f;
        m_DodgeSettings = FindObjectOfType<DodgeSettings>();
        agentRb = GetComponent<Rigidbody>();
        agentRb.maxAngularVelocity = 500;

        m_ResetParams = Academy.Instance.EnvironmentParameters;
        inventory = 0;
    }


    public void MoveAgent(ActionSegment<int> act)
    {
        var dirToGo = Vector3.zero;
        var rotateDir = Vector3.zero;

        m_KickPower = 0f;

        var forwardAxis = act[0];
        var rightAxis = act[1];
        var rotateAxis = act[2];
        var shoot = act[3];

        switch (forwardAxis)
        {
            case 1:
                dirToGo = transform.forward * m_ForwardSpeed;
                m_KickPower = 1f;
                break;
            case 2:
                dirToGo = transform.forward * -m_ForwardSpeed;
                break;
        }

        switch (rightAxis)
        {
            case 1:
                dirToGo = transform.right * m_LateralSpeed;
                break;
            case 2:
                dirToGo = transform.right * -m_LateralSpeed;
                break;
        }

        switch (rotateAxis)
        {
            case 1:
                rotateDir = transform.up * -1f;
                break;
            case 2:
                rotateDir = transform.up * 1f;
                break;
        }

        switch (shoot)
        {
            case 1:
                shootball();
                break;
        }

        transform.Rotate(rotateDir, Time.deltaTime * 100f);
        agentRb.AddForce(dirToGo * m_DodgeSettings.agentRunSpeed,
            ForceMode.VelocityChange);
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)

    {

        /*if (position == Position.Goalie) // DONT NEED FOR DODGEBALL
        {
            // Existential bonus for Goalies.
            AddReward(m_Existential);
        }
        else if (position == Position.Striker)
        {
            // Existential penalty for Strikers
            AddReward(-m_Existential);
        }*/
        AddReward(-m_Existential);
        MoveAgent(actionBuffers.DiscreteActions);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        //forward
        if (Input.GetKey(KeyCode.W))
        {
            discreteActionsOut[0] = 1;
        }
        if (Input.GetKey(KeyCode.S))
        {
            discreteActionsOut[0] = 2;
        }
        //rotate
        if (Input.GetKey(KeyCode.A))
        {
            discreteActionsOut[2] = 1;
        }
        if (Input.GetKey(KeyCode.D))
        {
            discreteActionsOut[2] = 2;
        }
        //right
        if (Input.GetKey(KeyCode.E))
        {
            discreteActionsOut[1] = 1;
        }
        if (Input.GetKey(KeyCode.Q))
        {
            discreteActionsOut[1] = 2;
        }
        if (Input.GetKey(KeyCode.Space))
        {
            discreteActionsOut[3] =1;
        }
    }
    /// <summary>
    /// Used to provide a "kick" to the ball.
    /// </summary>
    void OnCollisionEnter(Collision c) //MAYBE NEED TO FIX?? SHOULD ONLY GIVE POINTS IF THE BALL IS NEUTRAL?
    {
        var force = k_Power * m_KickPower;
        /*if (position == Position.Goalie) // DONT NEED FOR DODGEBALL
        {
            force = k_Power;
        }*/

        if (c.gameObject.CompareTag("ball"))
        {
            AddReward(.2f * m_BallTouch);
            if (c.gameObject.GetComponent<Dodgeball>().canpickup == 1)
            {
                pickupball(c.gameObject);
            }
            // var dir = c.contacts[0].point - transform.position;
            // dir = dir.normalized;
            // c.gameObject.GetComponent<Rigidbody>().AddForce(dir * force);
        }
    }

    public override void OnEpisodeBegin()
    {
        m_BallTouch = m_ResetParams.GetWithDefault("ball_touch", 0);
    }

    void pickupball(GameObject grabObject)
    {
        grabbedObject = grabObject;
        grabbedObjectSize = grabbedObject.GetComponent<Renderer>().bounds.size.magnitude;

        if (grabbedObject != null) {
            grabbedObject.SetActive(false);
            inventory++;
        }
    }

    void shootball()
    {
        if (inventory == 0) {
            return;
        }
        var ball = Instantiate(projectile, transform.position+(transform.forward*2), transform.rotation);
        
        ball.GetComponent<Rigidbody>().AddRelativeForce(new Vector3 (0, 0, 4000f));
        ball.GetComponent<Dodgeball>().area = (GameObject.Find("Game Environment"));
        if (gameObject.tag == "blueAgent")
        {
            ball.GetComponent<Dodgeball>().SetState(Dodgeball.BallState.blue);
        }
        else if (gameObject.tag == "purpleAgent")
        {
            ball.GetComponent<Dodgeball>().SetState(Dodgeball.BallState.purple);
        }
        
        inventory--;
    }

    void resetInventory()
    {
        inventory = 0;
    }

}
