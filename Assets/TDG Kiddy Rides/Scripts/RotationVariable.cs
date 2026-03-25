/* TDG Rotation Variable */
/* Rotates an object with an incremental speed up and slow down when powered on or off */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Movement {

    public class RotationVariable : MonoBehaviour {

        //[SerializeField] GameObject mainBody;
        [SerializeField] bool poweredOn = false;
        [SerializeField] bool running = false;
        [SerializeField] float speed, maxSpeed;
        [SerializeField] bool xRotate, yRotate, zRotate;

        private void Update() {
            if (poweredOn) {
                running = true;
                if (speed < maxSpeed) SpeedUp();
                MoveBody();
            } else {
                if (running) {
                    SlowDown();
                    MoveBody();
                }
            }
        }

        void SpeedUp() {
            speed += 0.05f;
            if (speed >= maxSpeed) speed = maxSpeed;
        }

        void SlowDown() {
            speed -= 0.05f;
            if (speed <= 0) {
                speed = 0;
                running = false;
            }
        }

        void MoveBody() {
            if (xRotate) transform.Rotate(new Vector3(speed, 0, 0) * Time.deltaTime);
            else if (yRotate) transform.Rotate(new Vector3(0, speed, 0) * Time.deltaTime);
            else if (zRotate) transform.Rotate(new Vector3(0, 0, speed) * Time.deltaTime);
        }

    }

}