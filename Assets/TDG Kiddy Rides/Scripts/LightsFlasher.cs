/* TDG Light Flasher */
/* Flashes the specified Lights on and off */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lighting {
    public class LightsFlasher : MonoBehaviour {

        [Header("Lights")]
        [SerializeField] GameObject light1;
        [SerializeField] GameObject light2;

        [Header("Materials")]
        [SerializeField] Material light1On;
        [SerializeField] Material light1Off;
        [SerializeField] Material light2On;
        [SerializeField] Material light2Off;

        [Header("Timer")]
        [SerializeField] float lightTime = 1f;
        [SerializeField] float lightTimer;
        bool lightDirection;

        private void Start() {
            lightTimer = lightTime;
        }

        private void Update() {
            lightTimer -= Time.deltaTime;
            if (lightTimer <= 0) {
                lightTimer = lightTime;
                ChangeLights();
            }
        }

        void ChangeLights() {
            if (lightDirection) {
                // Light 1 on, Light 2 off
                ChangeLight(light1, light1On);
                ChangeLight(light2, light2Off);
                lightDirection = !lightDirection;
            } else {
                // Light 1 off, Light 2 on
                ChangeLight(light1, light1Off);
                ChangeLight(light2, light2On);
                lightDirection = !lightDirection;
            }
        }

        void ChangeLight(GameObject lightToChange, Material materialTouse) {
            Material currentMat = lightToChange.GetComponent<MeshRenderer>().material;
            currentMat = materialTouse;
            lightToChange.GetComponent<MeshRenderer>().material = currentMat;
        }

    }
}
