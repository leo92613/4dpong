using UnityEngine;
using System.Collections;
using System;

namespace Holojam.IO
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class PongClient : MonoBehaviour
    {
        private Vector4 pos;
        private Color32 defaultcolor, towardscolor, futhercolor, hitcolor;
        private float scalefactor = 0.05f;
        private Vector4[] srcVertices, vertices;
        private Vector4 center = new Vector4(0, 1f, 0, 0);
        private Mesh mesh;
        private float X, Y, Z, W;
        private bool canHit, isDead;
        private float userperspective;

        public int controllerindex;
        public GameObject Sync1, Sync2;
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
        public void initvertices(Vector4 A_)
        {
            srcVertices = new Vector4[16];
            vertices = new Vector4[16];
            int n = 0;
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    for (int k = -1; k <= 1; k += 2)
                        for (int l = -1; l <= 1; l += 2)
                        {
                            vertices[n] = new Vector4((float)l * scalefactor, (float)k * scalefactor, (float)j * scalefactor, (float)i * scalefactor);// + center;
                            srcVertices[n++] = new Vector4((float)l * scalefactor, (float)k * scalefactor, (float)j * scalefactor, (float)i * scalefactor);//+ center;
                        }
        }

        public void setupperspective(float _f)
        {
            userperspective = _f;
        }

        public PongClient()
        {
            initvertices(center);
        }
        public PongClient(Vector4 A_)
        {
            center = A_;
            initvertices(center);
        }
        public Vector3 get3dver(int i)
        {
            float factor = userperspective / Mathf.Abs(vertices[i].w - userperspective);
            Vector3 rst;
            rst = new Vector3(vertices[i].x * factor, vertices[i].y * factor, vertices[i].z * factor);
            return rst;
        }
        public void updatepoint4(float[] src, int i)
        {
            vertices[i].x = (float)src[0];
            vertices[i].y = (float)src[1];
            vertices[i].z = (float)src[2];
            vertices[i].w = (float)src[3];
        }
        public void setupmesh(Mesh mesh)
        {
            Vector3[] _vertices;
            _vertices = new Vector3[16];
            for (int i = 0; i < 16; i++)
            {
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
        public void move(Vector4 movement)
        {
            for (int i = 0; i < 16; i++)
            {
                vertices[i] = srcVertices[i] + movement;
            }
            float factor = 2 / (2 + movement.w);
        }
        void checkboundary(Vector4 _pos)
        {
            // This part is for future multiplayer stuff
            if (Sync2.GetComponent<Transform>().position.y > 0)
            {
                Color32 _color = new Color32(85, 128, 0, 19);
                this.GetComponent<Renderer>().material.SetColor("_TintColor", hitcolor);
            }
            else if (Sync2.GetComponent<Transform>().position.y == 0)
            {
                if (Sync2.GetComponent<Transform>().position.z > 0)
                    this.GetComponent<Renderer>().material.SetColor("_TintColor", futhercolor);
                else
                    this.GetComponent<Renderer>().material.SetColor("_TintColor", defaultcolor);
            }

        }



        // Use this for initialization
        void Awake()
        {
            mesh = new Mesh();
            setupperspective(-3f);
            setupmesh(mesh);
            defaultcolor = new Color32(240, 122, 122, 19);
            futhercolor = new Color32(0, 0, 179, 19);
            hitcolor = new Color32(85, 128, 0, 19);
            Sync1.GetComponent<Transform>().position = new Vector3();
            Sync2.GetComponent<Transform>().position = new Vector3();

        }

        // Update is called once per frame
        void Update()
        {
            pos = new Vector4(Sync1.GetComponent<Transform>().position.x, Sync1.GetComponent<Transform>().position.y, Sync1.GetComponent<Transform>().position.z, Sync2.GetComponent<Transform>().position.x);
            move(pos);
            setupmesh(mesh);
        }
    }
}
