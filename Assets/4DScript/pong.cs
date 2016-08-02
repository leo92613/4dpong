using UnityEngine;
using System.Collections;
using System;

namespace Holojam.IO {
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class pong : GlobalReceiver, IGlobalTriggerPressDownHandler{
        [SerializeField]
        private Vector4 speed;
        private Vector4 pos = new Vector4();
        private Color32 defaultcolor,towardscolor,futhercolor,hitcolor;
        private float scalefactor = 0.05f;
        private Vector4[] srcVertices, vertices;
        private Vector4 center = new Vector4(0, 1f, 0, 0);
        private Mesh mesh;
        private float X, Y, Z, W;
        private bool canHit,isDead;
        private float userperspective;

        public int controllerindex;
        #region Face
        int[] faces = new int[] {4,0,8,12,
            6,2,10,14,
            5,1,9,13,
            7,3,11,15,
            2,0,8,10,
            6,4,12,14,
            3,1,9,11,
            7,5,13,15,
            2,0,4,6,
            10,8,12,14,
            3,1,5,7,
            11,9,13,15,
            1,0,8,9,
            5,4,12,13,
            3,2,10,11,
            7,6,14,15,
            1,0,4,5,
            9,8,12,13,
            3,2,6,7,
            11,10,14,15,
            1,0,2,3,
            9,8,10,11,
            5,4,6,7,
            13,12,14,15};
        #endregion



// Methods
        public void initvertices(Vector4 A_) {
            srcVertices = new Vector4[16];
            vertices = new Vector4[16];
            int n = 0;
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    for (int k = -1; k <= 1; k += 2)
                        for (int l = -1; l <= 1; l += 2) {
                            vertices[n] = new Vector4((float)l * scalefactor, (float)k * scalefactor, (float)j * scalefactor, (float)i * scalefactor);// + center;
                            srcVertices[n++] = new Vector4((float)l * scalefactor, (float)k * scalefactor, (float)j * scalefactor, (float)i * scalefactor);//+ center;
                        }
        }

        public void setupperspective(float _f) {
            userperspective = _f;
        }

        public pong() {
            initvertices(center);
        }
        public pong(Vector4 A_) {
            center = A_;
            initvertices(center);
        }
        public Vector3 get3dver(int i) {
            float factor = userperspective/ Mathf.Abs(vertices[i].w - userperspective);
            Vector3 rst;
            rst = new Vector3(vertices[i].x * factor, vertices[i].y * factor, vertices[i].z * factor) ;
            return rst;
        }
        public void updatepoint4(float[] src, int i) {
            vertices[i].x = (float)src[0];
            vertices[i].y = (float)src[1];
            vertices[i].z = (float)src[2];
            vertices[i].w = (float)src[3];
        }
        public void setupmesh(Mesh mesh) {
            Vector3[] _vertices;
            _vertices = new Vector3[16];
            for (int i = 0; i < 16; i++) {
                _vertices[i] = get3dver(i);
            }
            mesh.vertices = _vertices;
            mesh.SetIndices(faces, MeshTopology.Quads, 0);
            mesh.RecalculateBounds();
            mesh.Optimize();
            mesh.SetTriangles(mesh.GetTriangles(0), 0);
            GetComponent<MeshFilter>().mesh = mesh;
            GetComponent<MeshCollider>().sharedMesh = null;
            GetComponent<MeshCollider>().sharedMesh = mesh;
        }
        void initGame() {
            initspeed();
            isDead = false;
        }
        void endGame() {
            isDead = true;
            pos = new Vector4();
            move(pos);
            setupmesh(mesh);
            this.GetComponent<Renderer>().material.SetColor("_TintColor", defaultcolor);
        }
        public void move(Vector4 movement) {
            for (int i = 0; i < 16; i++) {
                vertices[i] = srcVertices[i]+movement;
            }
            float factor = 2 / (2 + movement.w);
        }

        void changespeed(Vector4 _speed) {
                speed += _speed;
            }

        void checkbounce(Vector4 _pos) {

            if (_pos.x > X) {
                pos.x = 2 * X - _pos.x;
                changespeed(new Vector4(2 * (-speed.x), 0, 0, 0));
            }
            else if (_pos.x < -X) {
                pos.x = 2 * (-X) - _pos.x;
                changespeed(new Vector4(2 * (-speed.x), 0, 0, 0));
            }
            else
                pos.x = _pos.x;


            if (_pos.y > Y) {
                pos.y = 2 * Y - _pos.y;
                changespeed(new Vector4(0, 2 * (-speed.y), 0, 0));
            }
            else if (_pos.y < -Y) {
                pos.y = 2 * (-Y) - _pos.y;
                changespeed(new Vector4(0, 2 * (-speed.y), 0, 0));
            }
            else
                pos.y = _pos.y;


            if (_pos.z > Z) {
                pos.z = 2 * Z - _pos.z;
                changespeed(new Vector4(0, 0, 2 * (-speed.z), 0));
            }
            else if (_pos.z < -Z) {
                pos.z = 2 * (-Z) - _pos.z;
                changespeed(new Vector4(0, 0, 2 * (-speed.z), 0));
            }
            else 
                pos.z = _pos.z;
        }

        void checkboundary(Vector4 _pos) {
            // This part is for future multiplayer stuff
            if (_pos.w <= -(0.8*W) && speed.w<0) {
                //canHit = true;
                //Color32 _color = new Color32(0, 0, 179, 19);
                //this.GetComponent<Renderer>().material.SetColor("_TintColor", _color);
            }
            else if (_pos.w >= (0.8 * W) && speed.w>0) {
                canHit = true;
                Color32 _color = new Color32(85, 128, 0,19);
                this.GetComponent<Renderer>().material.SetColor("_TintColor", hitcolor);
            }
           else { 
                canHit = false;
                if(speed.w < 0)
                    this.GetComponent<Renderer>().material.SetColor("_TintColor", futhercolor);
                else
                    this.GetComponent<Renderer>().material.SetColor("_TintColor", defaultcolor);
            }
                if (_pos.w > W) {
                    pos.w = 2 * W - _pos.w;
                    changespeed(new Vector4(0, 0, 0, 2 * (-speed.w)));
                    endGame();
            }
                if (_pos.w < -W) {
                    pos.w = 2 * (-W) - _pos.w;
                    changespeed(new Vector4(0, 0, 0, 2 * (-speed.w)));
                    //isout = true;
                }
            if (_pos.w >= -W && _pos.w <= W) {
                //canHit = false;
                checkbounce(_pos);
                pos.w = _pos.w;
            }
                //if (isout)
                //    StartCoroutine(pulse());

            }

        void initspeed() {
                float x, y, z, w;
                x = UnityEngine.Random.Range(0.5f, 0.8f);
                y = UnityEngine.Random.Range(0.5f, 0.8f);
                z = UnityEngine.Random.Range(0.5f, 0.8f);
                w = -0.2f;
                speed = new Vector4(x, y, z, w);
            }

        IEnumerator pulse(int _controllerindex) {
            SteamVR_Controller.Input(_controllerindex).TriggerHapticPulse(3000);
            Debug.Log(_controllerindex);
            yield return null;
            }

        void hit(Vector3 _speed) {
            //float speedscale = Mathf.Sqrt(speed.x *speed.x + speed.y*speed.y + speed.z*speed.z) / _speed.magnitude;
            float speedscale = 0.5f;
            initspeed();
            changespeed(new Vector4(_speed.x * speedscale, _speed.y * speedscale, _speed.z * speedscale, 0)) ;
        }
        
        // Use this for initialization
        void Awake() {
            mesh = new Mesh();
            setupperspective(3f);
            setupmesh(mesh);
            X = 1f;
            Y = 1f;
            Z = 1f;
            W = 1f;
            defaultcolor = new Color32(240, 122, 122, 19);
            futhercolor = new Color32(0, 0, 179, 19);
            hitcolor = new Color32(85, 128, 0, 19);
            isDead = true;
            canHit = false;
            
        }

        // Update is called once per frame
        void Update() {
            if (!isDead) {
                Vector4 tmp = new Vector4();
                tmp = pos + speed * Time.deltaTime;
                checkboundary(tmp);
                move(pos);
                setupmesh(mesh);
            }
        }
        void OnTriggerEnter(Collider other) {
            if (canHit) {
                if (other.gameObject.tag == "Player") {                   
                    StartCoroutine(pulse(controllerindex));
                    hit(other.GetComponent<ShowVelocity>().velocity);
                }
            }
        }

        public void OnGlobalTriggerPressDown(ViveControllerModule.EventData eventData) {
            controllerindex = (int)eventData.steamVRTrackedObject.index;
            if (isDead) {
                StartCoroutine(pulse(controllerindex));
                initGame();
            }
        }
    }
}
