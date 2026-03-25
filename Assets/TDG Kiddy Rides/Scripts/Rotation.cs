/* TDG Variable */
/* Rotates an object with some basic options */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Movement {

    public class Rotation : MonoBehaviour {

        [SerializeField] bool poweredOn = false;
        [SerializeField] float rotateSpeed, rotateMin, rotateMax;
        [SerializeField] bool direction = false;
        [SerializeField] bool dirX = false;
        [SerializeField] bool dirY = true;
        [SerializeField] bool dirZ = false;
        Vector3 rotationValue;

        private void Start() {
            rotateSpeed = Random.Range(rotateMin, rotateMax);
            SetupRotation();
        }

        void SetupRotation() {
            if (direction) { 
                if (dirX) rotationValue = new Vector3(rotateSpeed, 0, 0);
                else if (dirY) rotationValue = new Vector3(0, rotateSpeed, 0);
                else if (dirZ) rotationValue = new Vector3(0, 0, rotateSpeed);
            } else {
                if (dirX) rotationValue = new Vector3(-rotateSpeed, 0, 0);
                else if (dirY) rotationValue = new Vector3(0, -rotateSpeed, 0);
                else if (dirZ) rotationValue = new Vector3(0, 0, -rotateSpeed);
            }
        }

        private void Update() {            
            if (poweredOn) {
                transform.Rotate(rotationValue * Time.deltaTime);
            }
        }

    }

}