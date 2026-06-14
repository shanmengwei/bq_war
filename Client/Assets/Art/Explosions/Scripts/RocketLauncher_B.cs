using UnityEngine;
using System.Collections;

public class RocketLauncher_B : MonoBehaviour {


public GameObject engineFlames;
public GameObject LauncherMesh;
public GameObject LauncherAnim;
public GameObject SmokeParticles;
public GameObject Explosion;
public GameObject ExplosionAudio;

void Start (){

	 engineFlames.SetActive(false);
	 SmokeParticles.SetActive(false);
	 Explosion.SetActive(false);
	 ExplosionAudio.SetActive(false);
}

void Update (){

	if (Input.GetButtonDown("Fire1"))
    {

			StartCoroutine ("LaunchRocket");


    }

}

IEnumerator LaunchRocket (){

    engineFlames.SetActive(true);
    SmokeParticles.SetActive(true);
    Explosion.SetActive(true);

	yield return new WaitForSeconds (0.5f);



	LauncherAnim.GetComponent<Animation>().Play();

	yield return new  WaitForSeconds (4.2f);

	ExplosionAudio.SetActive(true);
	engineFlames.SetActive(false);

}
}