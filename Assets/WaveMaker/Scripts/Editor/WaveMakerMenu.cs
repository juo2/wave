using UnityEditor;
using UnityEngine;

namespace WaveMaker.Editor
{
    public class WaveMakerMenu : MonoBehaviour
    {
        [MenuItem("GameObject/3D Object/WaveMaker/Velocity Based (Simple)/Surface", false)]
        static void CreateWaveMakerSurfaceSimple(MenuCommand menuCommand)
        {
            var surface = CreateSurface(menuCommand.context as GameObject);
            surface.interactionType = WaveMakerSurface.InteractionType.VelocityBased;
        }

        [MenuItem("GameObject/3D Object/WaveMaker/Occupancy Based (Advanced)/Surface", false)]
        static void CreateWaveMakerSurfaceBuoyancy(MenuCommand menuCommand)
        {
            var surface = CreateSurface(menuCommand.context as GameObject);
            Selection.activeObject = surface.gameObject;
            surface.interactionType = WaveMakerSurface.InteractionType.OccupancyBased;
            surface.nMaxInteractorsDetected = 3;
        }


        [MenuItem("GameObject/3D Object/WaveMaker/Velocity Based (Simple)/Interactor (Sphere)", false)]
        static void CreateWaveMakerInteractorSphereVelocity(MenuCommand menuCommand)
        {
            var go = CreateSphereInteractor(menuCommand.context as GameObject);
            var rb = go.GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        [MenuItem("GameObject/3D Object/WaveMaker/Occupancy Based (Advanced)/Interactor (Sphere)", false)]
        static void CreateWaveMakerInteractorSphereOccupancy(MenuCommand menuCommand)
        {
            var go = CreateSphereInteractor(menuCommand.context as GameObject);
            var rb = go.GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 2f;
            rb.angularDrag = 1;
        }

        [MenuItem("GameObject/3D Object/WaveMaker/Velocity Based (Simple)/Interactor (Cube)", false)]
        static void CreateWaveMakerInteractorBoxVelocity(MenuCommand menuCommand)
        {
            var go = CreateBoxInteractor(menuCommand.context as GameObject);
            var rb = go.GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        [MenuItem("GameObject/3D Object/WaveMaker/Occupancy Based (Advanced)/Interactor (Cube)", false)]
        static void CreateWaveMakerInteractorBoxOccupancy(MenuCommand menuCommand)
        {
            var go = CreateBoxInteractor(menuCommand.context as GameObject);
            var rb = go.GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 3f;
            rb.angularDrag = 1;
        }

        [MenuItem("GameObject/3D Object/WaveMaker/Velocity Based (Simple)/Interactor (Capsule)", false)]
        static void CreateWaveMakerInteractorCapsuleVelocity(MenuCommand menuCommand)
        {
            var go = CreateCapsuleInteractor(menuCommand.context as GameObject);
            var rb = go.GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        [MenuItem("GameObject/3D Object/WaveMaker/Occupancy Based (Advanced)/Interactor (Capsule)", false)]
        static void CreateWaveMakerInteractorCapsuleOccupancy(MenuCommand menuCommand)
        {
            var go = CreateCapsuleInteractor(menuCommand.context as GameObject);
            var rb = go.GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 2f;
            rb.angularDrag = 1;
        }

        /********* STATIC FUNCTIONS ****************************************************/

        static WaveMakerSurface CreateSurface(GameObject parent = null)
        {
            var go = new GameObject("WaveMaker Surface");

            go.AddComponent<MeshFilter>();

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.material = Resources.Load("Materials/WaveMakerWaveMaterial", typeof(Material)) as Material;

            var surface = go.AddComponent<WaveMakerSurface>();
            GameObjectUtility.SetParentAndAlign(go, parent);
            Undo.RegisterCreatedObjectUndo(go, "Create WaveMaker Surface: " + go.name);
            Utils.Log("GameObject created. Create a WaveMaker Descriptor file on the project view and attach to the component to start.", go);

            Selection.activeObject = go;

            return surface;
        }

        static GameObject CreateSphereInteractor(GameObject parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "WaveMaker Interactor - Sphere";
            CreateInteractor(go, parent);
            return go;
        }

        static GameObject CreateBoxInteractor(GameObject parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "WaveMaker Interactor - Box";
            CreateInteractor(go, parent);
            return go;
        }


        static GameObject CreateCapsuleInteractor(GameObject parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "WaveMaker Interactor - Capsule";
            CreateInteractor(go, parent);
            return go;
        }

        static void CreateInteractor(GameObject go, GameObject parent = null)
        {
            go.AddComponent<Rigidbody>();
            go.AddComponent<WaveMakerInteractor>();
            GameObjectUtility.SetParentAndAlign(go, parent);
            Undo.RegisterCreatedObjectUndo(go, "WaveMaker Interactor created");
            Utils.Log("Interactor created", go);
            go.GetComponent<Collider>().isTrigger = false;
            Selection.activeObject = go;
        }
    }
}