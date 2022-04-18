using System.Linq;
using TensorFlowLite;
using UnityEngine;
using UnityEngine.UI;

// [RequireComponent(typeof(WebCamInput))]
public sealed class FaceMeshSample : MonoBehaviour
{
    [SerializeField, FilePopup("*.tflite")]
    private string faceModelFile = null;

    [SerializeField, FilePopup("*.tflite")]
    private string faceMeshModelFile = null;

    [SerializeField]
    private bool useLandmarkToDetection = true;

    [SerializeField]
    private RawImage cameraView = null;

    [SerializeField]
    private RawImage croppedView = null;

    [SerializeField]
    private Material faceMaterial = null;

    [SerializeField]
    public Texture2D image = null;

    private FaceDetect faceDetect;
    private FaceMesh faceMesh;
    private PrimitiveDraw draw;
    private MeshFilter faceMeshFilter;
    private Vector3[] faceKeypoints;
    private FaceDetect.Result detectionResult;
    private FaceMesh.Result meshResult;
    private readonly Vector3[] rtCorners = new Vector3[4];

    private void Start()
    {
        faceDetect = new FaceDetect(faceModelFile);
        faceMesh = new FaceMesh(faceMeshModelFile);
        draw = new PrimitiveDraw(Camera.main, gameObject.layer);

        // Create Face Mesh Renderer
        {
            var go = new GameObject("Face");
            go.transform.SetParent(transform);
            var faceRenderer = go.AddComponent<MeshRenderer>();
            faceRenderer.material = faceMaterial;

            faceMeshFilter = go.AddComponent<MeshFilter>();
            faceMeshFilter.sharedMesh = FaceMeshBuilder.CreateMesh();

            faceKeypoints = new Vector3[FaceMesh.KEYPOINT_COUNT];
        }

        // string imagePath = Application.streamingAssetsPath + "/data/enhanced/enhanced_100.png";
        // var rawData = System.IO.File.ReadAllBytes(imagePath);
        // Texture2D image = new Texture2D(2, 2);
        // image.LoadImage(rawData);
        image = Resize(image, 256, 256);
        OnTextureUpdate(image);
        //FindObjectOfType<UnityEngine.UI.RawImage>().texture = image;
        GameObject rawImage = GameObject.Find("RawImage");
        rawImage.GetComponent<RawImage>().texture = image;

        // var webCamInput = GetComponent<WebCamInput>();
        // webCamInput.OnTextureUpdate.AddListener(OnTextureUpdate);
    }

    private static Texture2D Resize(Texture2D texture2D,int targetX,int targetY)
    {
        RenderTexture rt=new RenderTexture(targetX, targetY,24);
        RenderTexture.active = rt;
        Graphics.Blit(texture2D,rt);
        Texture2D result=new Texture2D(targetX,targetY);
        result.ReadPixels(new Rect(0,0,targetX,targetY),0,0);
        result.Apply();
        return result;
    }

    private void OnDestroy()
    {
        // var webCamInput = GetComponent<WebCamInput>();
        // webCamInput.OnTextureUpdate.RemoveListener(OnTextureUpdate);

        faceDetect?.Dispose();
        faceMesh?.Dispose();
        draw?.Dispose();
    }

    private void Update()
    {
        DrawResults(detectionResult, meshResult);
    }

    private void OnTextureUpdate(Texture texture)
    {
        if (detectionResult == null || !useLandmarkToDetection)
        {
            faceDetect.Invoke(texture);
            cameraView.material = faceDetect.transformMat;
            detectionResult = faceDetect.GetResults().FirstOrDefault();
            Debug.Log($"detection {(detectionResult is null?"failed":"succeeded")}");
            if (detectionResult == null)
            {
                return;
            }
        }

        faceMesh.Invoke(texture, detectionResult);
        croppedView.texture = faceMesh.inputTex;
        meshResult = faceMesh.GetResult();

        if (meshResult.score < 0.5f)
        {
            detectionResult = null;
            return;
        }

        if (useLandmarkToDetection)
        {
            detectionResult = faceMesh.LandmarkToDetection(meshResult);
        }
    }

    private void DrawResults(FaceDetect.Result detection, FaceMesh.Result face)
    {
        cameraView.rectTransform.GetWorldCorners(rtCorners);
        Vector3 min = rtCorners[0];
        Vector3 max = rtCorners[2];

        // Draw Face Detection
        if (detection != null)
        {
            draw.color = Color.blue;
            Rect rect = MathTF.Lerp(min, max, detection.rect, true);
            draw.Rect(rect, 0.05f);
            foreach (Vector2 p in detection.keypoints)
            {
                draw.Point(MathTF.Lerp(min, max, new Vector3(p.x, 1f - p.y, 0)), 0.1f);
            }
            draw.Apply();
        }

        if (face != null)
        {
            // Draw face
            draw.color = Color.green;
            float zScale = (max.x - min.x) / 2;
            for (int i = 0; i < face.keypoints.Length; i++)
            {
                Vector3 kp = face.keypoints[i];
                kp.y = 1f - kp.y;

                Vector3 p = MathTF.Lerp(min, max, kp);
                p.z = face.keypoints[i].z * zScale;

                faceKeypoints[i] = p;
                draw.Point(p, 0.05f);
            }
            draw.Apply();

            // Update Mesh
            FaceMeshBuilder.UpdateMesh(faceMeshFilter.sharedMesh, faceKeypoints);
        }
    }
}
