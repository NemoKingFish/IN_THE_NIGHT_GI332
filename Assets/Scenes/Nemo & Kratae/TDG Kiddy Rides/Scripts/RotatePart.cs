/* TDG Rotate part */
/* Rotates the specified transform(s) by a random amount, set by the configurable range */
/* Used when the script is on a parent object to rotate a child object */

using UnityEngine;

namespace Movement {

    public class RotatePart : MonoBehaviour {

        [SerializeField] GameObject[] partsToRotate;
        [SerializeField] bool poweredOn = false;
        [SerializeField] float rotateSpeed, rotateMin, rotateMax;
        [SerializeField] bool direction = false;

        private void Start() {
            rotateSpeed = Random.Range(rotateMin, rotateMax);
        }

        private void Update() {
            foreach (GameObject movingPart in partsToRotate) {
                if (poweredOn) {
                    if (direction) movingPart.transform.Rotate(new Vector3(rotateSpeed, 0, 0) * Time.deltaTime);
                    else movingPart.transform.Rotate(new Vector3(-rotateSpeed, 0, 0) * Time.deltaTime);
                }
            }
        }

    }

}