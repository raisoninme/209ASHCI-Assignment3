using System;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.Networking;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.Input;
using TMPro;

namespace Oculus.Interaction.HandPosing
{
    public class DataCollection : MonoBehaviour
    {
        
        static DataCollection instance;

        [Header("Participant Index (e.g., P0)")]
        public string _participant = "P0"; // type in P0, P1,P2 ... to help you distinguish data from different users

        [Header("Enable Logging")] 
        public bool enable = true; // this script only collects data when enable is true
        [SerializeField] private HandGrabInteractor handGrab; // drag the dominant hand into this blank in the inspector
        
        [SerializeField] private GameObject grabbableCube; // drag the grabbableCube into this blank in the inspector
        [SerializeField] private GameObject targetCube; // drag the target cube into this blank in the inspector
        [SerializeField] private TextMeshPro hintText; // drag the hintText into this blank in the inspector 

        private bool isGrabbed = false; // if the object is grabbed this frame, isGrabbed is true
        private bool wasGrabbed = false; // if the object was grabbed last frame, wasGrabbed is true
        private bool isStart = false; // true when starting to grab
        private bool isEnd = false; // true when starting to release
        private float grabTime = Mathf.Infinity; // elapsed time of moving the grabbableCube from point A to point B
        private float grabDistance = Mathf.Infinity; // the distance between point A to point B
        private float grabWidth = Mathf.Infinity; // the size of the grabbableCube
        private float initialPos; // the initial position of the grabbableCube
        private float initialTime; // the initial timestamp of user interaction (moving the grabbableCube from A to B)
        private float startPositionX = -0.2f; // the start position of the grabbableCube

        private float moveSpeed = 2f; // the speed of the grabbableCube moving back to the original position
        private Vector3 originalPosition; // the original position of the grabbableCube

        private int grabWidthCounter = 1; // count the number of grabbableCubes, need 3 in total
        private int grabDistanceCounter = 1; // count the number of distances, need 3 in total
        private int grabTrialCounter = 1; // count the number of grabTimes, need 10 in total

        private StreamWriter _writer; // to write data into a file
        private string _filename; // the name of the file

        // Ensure this script will run across scenes. Do not delete it.
        private void Awake()
        {
            
            if (instance != null)
            {
                Destroy(gameObject);
            }
            else{
                instance = this;
                DontDestroyOnLoad(gameObject);
            }

        }

        // Save all data into the file when we quit the app
        private void OnApplicationQuit() {
            Debug.Log("On application quit");
            if (_writer != null) {
                _writer.Flush();
                _writer.Close();
                _writer.Dispose();
                _writer = null;
            }
        }
        
        // Save all data into the file when we pause the app
        private void OnApplicationPause(bool pauseStatus)
        {
            Debug.Log("On application pause");
            if (_writer != null) {
                _writer.Flush();
                _writer.Close();
                _writer.Dispose();
                _writer = null;
            }
        }
        
        private void Start()
        {
            // Create a csv file to save the data
            string filename = $"{_participant}-{Now}.csv";
            string path = Path.Combine(Application.persistentDataPath, filename);
            // if you run it in the Unity Editor on Windows, the path is %userprofile%\AppData\LocalLow\<companyname>\<productname>
            // if you run it on Mac, the path is the ~/Library/Application Support/company name/product name
            // if you run it on a standalone VR headset, the path is Oculus/Android/data/<packagename>/files
            // reference here: https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html
            // Attempt to create StreamWriter
            _writer = new StreamWriter(path);
            string msg = $"grabTime" +
                        $"grabWidth" +
                        $"grabDistance";
            _writer.WriteLine(msg);
            Debug.Log(msg);
            _writer.Flush();

            originalPosition = grabbableCube.transform.position;
            hintText.text = "Grab the cube and move it to the target cube.";
        }

        void Update()
        {
            // only collect data when enable is true
            if (enable == false) return;
            
            // read the grabbableCube width
            grabWidth = grabbableCube.transform.localScale.x;
            grabDistance = Mathf.Abs(startPositionX - targetCube.transform.position.x);

            // read the grab status
            isGrabbed = (InteractorState.Select == handGrab.State);
            // print("isGrabbed: "+ isGrabbed);
            isStart = !wasGrabbed && isGrabbed;
            // print("isStart: "+ isStart);
            isEnd = wasGrabbed && !isGrabbed;
            // print("isEnd: "+ isEnd);
            
            // if the overlap between the grabbableCube and the targetCube is more than 80%, change the color of the targetCube to green
            if (Mathf.Abs(grabbableCube.transform.position.x - targetCube.transform.position.x) < 0.2 * grabWidth){
                targetCube.GetComponent<Renderer>().material.color = Color.green;
            }
            else{
                targetCube.GetComponent<Renderer>().material.color = Color.white;
            }

            // start counting time and distance once a user grabs the grabbableCube
            if (isStart){
                initialPos = grabbableCube.transform.position.x;
                initialTime = Time.time;
                hintText.text = "distance:"+grabDistance.ToString()+"\n width:"+grabWidth.ToString()+ "\n trial:"+ grabTrialCounter.ToString() ;
            }
            // stop counting time and distance once a user releases the grabbableCube
            if (isEnd){
                // check the overlap between the grabbableCube and the targetCube
                if (Mathf.Abs(grabbableCube.transform.position.x - targetCube.transform.position.x) < 0.2 * grabWidth){
                    grabTime = Time.time - initialTime;
                    WriteToFile(grabTime, grabWidth, grabDistance);
                    if(grabTrialCounter < 10){
                        grabTrialCounter += 1;
                    }
                    else{
                        grabTrialCounter = 1;
                        if(grabDistanceCounter < 3){
                            grabDistanceCounter += 1;
                            // move the targetCube to the next distance according to the counter
                            switch(grabDistanceCounter){
                                case 2:
                                    targetCube.transform.position = new Vector3(0.1f, grabbableCube.transform.position.y, grabbableCube.transform.position.z);
                                    break;
                                case 3:
                                    targetCube.transform.position = new Vector3(0f, grabbableCube.transform.position.y, grabbableCube.transform.position.z);
                                    break;
                            }
                        }
                        else{
                            grabDistanceCounter = 1;
                            targetCube.transform.position = new Vector3(0.2f, grabbableCube.transform.position.y, grabbableCube.transform.position.z);
                            if(grabWidthCounter < 3){
                                grabWidthCounter += 1;
                                // change the size of the grabbableCube and the targetCube according to the counter
                                grabbableCube.transform.localScale = new Vector3(grabbableCube.transform.localScale.x + 0.02f, grabbableCube.transform.localScale.y, grabbableCube.transform.localScale.z);
                                targetCube.transform.localScale = new Vector3(targetCube.transform.localScale.x + 0.02f, targetCube.transform.localScale.y, targetCube.transform.localScale.z);
                            }
                            else{
                                hintText.text = "You have finished the experiment. Thank you!";
                            }
                        }
                    }
                }
            }   

            // if the grabbableCube is not grabbed, move it back to the original position
            if(!isGrabbed && grabbableCube.transform.position != originalPosition){
                ReturnToOriginal();
            }

            wasGrabbed = isGrabbed;
            
        }
        
        // write T, W, D into the file.
        private void WriteToFile(float grabTime, float grabWidth, float grabDistance) {
            if (_writer == null){
                Debug.Log("Writer is null");
                return;
            }
            
            string msg = $"{grabTime}," +
                        $"{grabWidth}," +
                        $"{grabDistance}";
            _writer.WriteLine(msg);
            Debug.Log("test msg: "+msg);
            _writer.Flush();
        }
        
        // generate the current timestamp for filename
        private double Now {
            get {
                System.DateTime epochStart = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
                return (System.DateTime.UtcNow - epochStart).TotalMilliseconds;
            }
        }

        private void ReturnToOriginal()
        {
            // move the grabbableCube back to the original position after end grabbing
            grabbableCube.transform.position = Vector3.MoveTowards(grabbableCube.transform.position, originalPosition, moveSpeed * Time.deltaTime);
        }
    
    }
}