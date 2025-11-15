using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Check this out we can require components be on a game object!
[RequireComponent(typeof(MeshFilter))]

public class BParticleSimMesh : MonoBehaviour
{
    public struct BSpring
    {
        public float kd;                        // damping coefficient
        public float ks;                        // spring coefficient
        public float restLength;                // rest length of this spring
        public int attachedParticle;            // index of the attached other particle (use me wisely to avoid doubling springs and sprign calculations)
    }

    public struct BContactSpring
    {
        public float kd;                        // damping coefficient
        public float ks;                        // spring coefficient
        public float restLength;                // rest length of this spring (think about this ... may not even be needed o_0
        public Vector3 attachPoint;             // the attached point on the contact surface
    }

    public struct BParticle
    {
        public Vector3 position;                // position information
        public Vector3 velocity;                // velocity information
        public float mass;                      // mass information
        public BContactSpring contactSpring;    // Special spring for contact forces
        public bool attachedToContact;          // is thi sparticle currently attached to a contact (ground plane contact)
        public List<BSpring> attachedSprings;   // all attached springs, as a list in case we want to modify later fast
        public Vector3 currentForces;           // accumulate forces here on each step        
    }

    public struct BPlane
    {
        public Vector3 position;                // plane position
        public Vector3 normal;                  // plane normal
    }

    public float contactSpringKS = 1000.0f;     // contact spring coefficient with default 1000
    public float contactSpringKD = 20.0f;       // contact spring daming coefficient with default 20

    public float defaultSpringKS = 100.0f;      // default spring coefficient with default 100
    public float defaultSpringKD = 1.0f;        // default spring daming coefficient with default 1

    public bool debugRender = true;            // To render or not to render


    /*** 
     * I've given you all of the above to get you started
     * Here you need to publicly provide the:
     * - the ground plane transform (Transform)
     * - handlePlaneCollisions flag (bool)
     * - particle mass (float)
     * - useGravity flag (bool)
     * - gravity value (Vector3)
     * Here you need to privately provide the:
     * - Mesh (Mesh)
     * - array of particles (BParticle[])
     * - the plane (BPlane)
     ***/
    public Transform groundPlaneTransform;
    public bool handlePlaneCollisions = true;
    public float particleMass = 1.0f;
    public bool useGravity = true;
    public Vector3 gravity = new Vector3(0.0f, -9.8f, 0.0f);
    private Mesh mesh;
    private BParticle[] particles;
    private BPlane groundPlane;

    /// <summary>
    /// Init everything
    /// HINT: in particular you should probbaly handle the mesh, init all the particles, and the ground plane
    /// HINT 2: I'd for organization sake put the init particles and plane stuff in respective functions
    /// HINT 3: Note that mesh vertices when accessed from the mesh filter are in local coordinates.
    ///         This script will be on the object with the mesh filter, so you can use the functions
    ///         transform.TransformPoint and transform.InverseTransformPoint accordingly 
    ///         (you need to operate on world coordinates, and render in local)
    /// HINT 4: the idea here is to make a mathematical particle object for each vertex in the mesh, then connect
    ///         each particle to every other particle. Be careful not to double your springs! There is a simple
    ///         inner loop approach you can do such that you attached exactly one spring to each particle pair
    ///         on initialization. Then when updating you need to remember a particular trick about the spring forces
    ///         generated between particles. 
    /// </summary>
    void Start()
    { 
        InitParticles();
        InitPlane();
    }



    /*** BIG HINT: My solution code has as least the following functions
     * InitParticles()
     * InitPlane()
     * UpdateMesh() (remember the hint above regarding global and local coords)
     * ResetParticleForces()
     * ...
     ***/

     public void InitParticles()
    {
        Mesh ogMesh = GetComponent<MeshFilter>().sharedMesh;
        mesh = Instantiate(ogMesh);
        GetComponent<MeshFilter>().mesh = mesh;

        Vector3[] vertices = mesh.vertices;
        int vertexCount = vertices.Length;

        particles = new BParticle[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            particles[i].position = transform.TransformPoint(vertices[i]);
            particles[i].velocity = Vector3.zero;
            particles[i].mass = particleMass;
            particles[i].attachedSprings = new List<BSpring>();
            particles[i].currentForces = Vector3.zero;
            particles[i].attachedToContact = false;


        }
        for(int i = 0; i < particles.Length; i++)
        {
            for(int j = i + 1; j < particles.Length; j++)
            {
                BSpring spring = new BSpring();
                spring.ks = defaultSpringKS;
                spring.kd = defaultSpringKD;
                spring.restLength = Vector3.Distance(particles[i].position, particles[j].position);
                spring.attachedParticle = j;
                particles[i].attachedSprings.Add(spring);
            }
        }
        
    }

    public void InitPlane()
    {
        groundPlane.position = groundPlaneTransform.position;
        groundPlane.normal = groundPlaneTransform.up;
    }

    public void CalculateCurrentForces()
    {
        
        for (int i = 0; i < particles.Length; i++)
        {
            for(int j = 0; j < particles[i].attachedSprings.Count; j++)
            {

                
               
                
                
                particles[i] = particles[i];
                BSpring spring = particles[i].attachedSprings[j];
                BParticle attachedParticle = particles[spring.attachedParticle];
                Vector3 direction = particles[i].position - attachedParticle.position;
                Vector3 velocity = particles[i].velocity - attachedParticle.velocity;
                float distance = direction.magnitude;
                direction.Normalize();
                if (distance <= 0) continue;

                Vector3 springForce = spring.ks * (spring.restLength - distance) * direction;
                Vector3 dampingForce = spring.kd * Vector3.Dot(velocity, direction) * direction;
                
                particles[i].currentForces += (springForce - dampingForce);
                particles[spring.attachedParticle].currentForces += -(springForce - dampingForce);
            }
           
        }
        
    }
    public void CalculateGroundPenalty()
    {
        
        Vector3 planeNormal = groundPlane.normal;
        if (handlePlaneCollisions)
        {
           for(int i = 0; i < particles.Length; i++)
           {
                
                float distanceToPlane = Vector3.Dot((particles[i].position - groundPlane.position), planeNormal);
                if (distanceToPlane < Mathf.Epsilon)
                {
                    if (!particles[i].attachedToContact)
                    {
                        particles[i].attachedToContact = true;
                        particles[i].contactSpring.ks = contactSpringKS;
                        particles[i].contactSpring.kd = contactSpringKD;
                        particles[i].contactSpring.attachPoint = particles[i].position - distanceToPlane * planeNormal;
                    }

                    Vector3 springForce = -contactSpringKS * distanceToPlane * planeNormal;
                    Vector3 dampingForce = -contactSpringKD * particles[i].velocity;
                    Vector3 contactForce = springForce + dampingForce;
                    if (Vector3.Dot(contactForce, planeNormal) > 0)
                    {
                        particles[i].currentForces += contactForce;

                    }
                }
                else
                {
                    particles[i].attachedToContact = false;
                }
                
                   
           }
        }
    }
    public void Intergrate()
    {
        
        for(int i = 0; i < particles.Length; i++)
        {
            
            Vector3 acceleration = particles[i].currentForces / particles[i].mass;
            particles[i].velocity += acceleration * Time.fixedDeltaTime;
            particles[i].position += particles[i].velocity * Time.fixedDeltaTime;
        }
    }
    public void UpdateMesh()
    {
        Vector3[] vertices = mesh.vertices;
        int vertexCount = vertices.Length;
        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i] = transform.InverseTransformPoint(particles[i].position);           
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
    public void ResetParticleForces()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].currentForces = Vector3.zero;
        }
    }

    public void UseGravity()
    {
        if (useGravity)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].currentForces += gravity * particles[i].mass;
            }
        }
    }

    /// <summary>
    /// Draw a frame with some helper debug render code
    /// </summary>
    public void Update()
    {
        /* This will work if you have a correctly made particles array*/
        if (debugRender)
        {
            int particleCount = particles.Length;
            for (int i = 0; i < particleCount; i++)
            {
                Debug.DrawLine(particles[i].position, particles[i].position + particles[i].currentForces, Color.blue);

                int springCount = particles[i].attachedSprings.Count;
                for (int j = 0; j < springCount; j++)
                {
                    Debug.DrawLine(particles[i].position, particles[particles[i].attachedSprings[j].attachedParticle].position, Color.red);
                }
            }
        }

       

    }
    public void FixedUpdate()
    {

        ResetParticleForces();
        CalculateCurrentForces();
        UseGravity();
        CalculateGroundPenalty();
        Intergrate();
        UpdateMesh();
        
    }
}


