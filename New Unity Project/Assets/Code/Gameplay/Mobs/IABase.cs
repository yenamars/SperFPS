﻿using UnityEngine;
using System.Collections;

public class IABase : MonoBehaviour {

	private enum IAActions
	{
		wait = 0,
		move = 1,
		search = 2,
		hit = 3,
		look = 4
	}
			
	public IABaseData data;
	public Transform[] eyeSlots;
	public Transform[] weaponsSlots;

	[HideInInspector]
	public IATarget target;
	[HideInInspector]
	public Vector3 lastKnownPosition;
	[HideInInspector]
	public bool detected;

	private IAActions iaActions;
	private IAEye[] eyes;
	private Player[] players;
	private Transform trsf;
	private IAMovement iaMovement;
	private Damageable damageable;
	private Rigidbody rgdBody;
	private IABody iaBody;
	private IAWeapon[] iaWeapons;
	private Vector3 searchPosition;
	private bool goingToSearchPosition;
	private bool strafing;
	private bool shoot;
	private bool dying;
	private bool stuned;
	private bool blind;

	public void SetUpIA () {
		players = GameManager.instance.players;
		trsf = transform;
		trsf.SetParent(GameManager.instance.mobsGroup);
		eyes = GetComponentsInChildren<IAEye>();
		if(eyes.Length == 0)
		{
			blind = true;
		}
		else
		{
			blind = false;
			for(int i = 0; i < eyes.Length; i++)
			{
				eyes[i].iaBase = this;
			}
		}
		target = players[0].iaTarget;
		iaMovement = GetComponent<IAMovement>();
		damageable = GetComponentInChildren<Damageable>();
		damageable.maxLifePoint = data.maxLifePoint;
		iaBody = GetComponentInChildren<IABody>();
		iaBody.orientationSpeed = data.orientationSpeed;
		rgdBody = GetComponent<Rigidbody>();
		iaWeapons = GetComponentsInChildren<IAWeapon>();
		iaBody.target = target;

		ResetVariables();
	}

	void Update()
	{
		if(damageable.dead == true)
		{
			Death();
			return;
		}

		if(damageable.takeDamage == true)
		{
			if(data.stunDuration*damageable.stuning > 0.0f)
			{
				StopAllCoroutines();
				StartCoroutine(HitCoroutine(damageable.pushBack, damageable.stuning, damageable.damageDirection));
				iaActions = IAActions.hit;
				strafing = false;
				lastKnownPosition = target.position;
				iaActions = IAActions.move;
			}
			damageable.takeDamage = false;
		}
		else if(stuned == false)
		{
			Detection();
		}

		switch ((int)iaActions)
		{
			case 0 :
			{
				Wait();
				break;
			}
			case 1 :
			{
				Move();
				break;
			}
			case 2 :
			{
				Search();
				break;
			}
			case 3 :
			{
				break;
			}
			case 4 :
			{
				Look();
				break;
			}
		}
	}

	void Detection()
	{
		detected = false;

		float distanceFromPlayer = (target.position - trsf.position).sqrMagnitude;

		if(target.soundProduced*target.soundProduced*data.hearing > distanceFromPlayer && target.player.isAlive == true)
		{
			lastKnownPosition = target.position;
			detected = true;
			iaActions = IAActions.move;
			if(blind)
			{
				shoot = true;
			}
		}
		else if(blind == false)
		{
			for(int i = 0; i < eyes.Length; i++)
			{
				if(eyes[i].detected == true && target.player.isAlive == true)
				{
					lastKnownPosition = target.position;
					detected = true;
					shoot = true;
					iaActions = IAActions.move;
					break;
				}
				else
				{
					shoot = false;
				}
			}
		}

		Shoot((lastKnownPosition - trsf.position).sqrMagnitude);
	}

	void Wait()
	{
		iaBody.look = false;
		StopAllCoroutines();
		iaMovement.Stop();
	}

	void Look()
	{
		iaBody.look = true;
		StopAllCoroutines();
		iaMovement.Stop();
		if(detected == false)
		{
			iaActions = IAActions.move;
		}
	}

	void Move()
	{
		iaBody.look = true;
		if(detected == true)
		{
			iaBody.lookAtTarget = true;
		}
		else
		{
			iaBody.lookAtTarget = false;
		}
		iaMovement.Resume();
		iaMovement.GotoDestination(lastKnownPosition);
		if(iaMovement.isAtDestination() == true && detected == false)
		{
			iaActions = IAActions.search;
		}
			
		if(data.canStrafe == true)
		{
			if((trsf.position - target.position).sqrMagnitude < data.startStrafingDisance*data.startStrafingDisance && strafing == false)
			{
				StartCoroutine(StrafeCoroutine());
			}
		}
	}

	void Shoot(float distance)
	{
		for(int i = 0; i < iaWeapons.Length; i++)
		{
			iaWeapons[i].distanceFromPlayer = distance;
			iaWeapons[i].targetDetected = shoot;
			iaWeapons[i].reactionTime = data.reactionTime;
			if(data.canMoveWhileShooting == false && shoot == true && iaWeapons[i].used == true)
			{
				strafing = false;
				iaActions = IAActions.look;
			}
		}
	}

	void Search()
	{
		shoot = false;
		iaBody.look = true;
		iaBody.lookAtTarget = false;
		if(goingToSearchPosition == false)
		{
			StartCoroutine(SearchPathCoroutine());
		}
		else if(iaMovement.isAtDestination())
		{
			goingToSearchPosition = false;
			iaMovement.Stop();
		}

		iaMovement.GotoDestination(searchPosition);
	}

	IEnumerator SearchPathCoroutine()
	{
		goingToSearchPosition = true;

		float r = Random.Range(data.searchingDistance.x, data.searchingDistance.y);
		Vector3 randomPosition = lastKnownPosition + new Vector3(Random.Range(-1.0f,1.0f), 0.0f, Random.Range(-1.0f,1.0f)).normalized * r;
		NavMeshHit hit;
		if(NavMesh.SamplePosition(randomPosition, out hit, 1.0f, NavMesh.AllAreas))
		{
			searchPosition = hit.position;
		}
		else
		{
			searchPosition = lastKnownPosition;
		}

		yield return new WaitForSeconds(Random.Range(data.searchingTimeRange.x, data.searchingTimeRange.y));

		iaMovement.Resume();
	}

	IEnumerator StrafeCoroutine()
	{
		strafing = true;
		yield return new WaitForSeconds(Random.Range(data.strafeFrequency.x, data.strafeFrequency.y));
		WaitForEndOfFrame wait = new WaitForEndOfFrame();
		float r = Random.Range(data.strafeSpeed.x, data.strafeSpeed.y);
		Vector3 strafeDir = (iaBody.transform.right*Random.Range(-1.0f,1.0f)).normalized*r;
		float value = 0.0f;
		float t = Random.Range(data.strafeDuration.x, data.strafeDuration.y);
		while(value < t)
		{
			value += Time.deltaTime;
			float ev = data.strafeCurve.Evaluate(value/t);
			iaMovement.Strafe(strafeDir*Time.deltaTime*ev);
			yield return wait;
		}
		strafing = false;
	}

	IEnumerator HitCoroutine(float projPushBack, float projStunDuration, Vector3 dir)
	{
		stuned = true;
		WaitForEndOfFrame wait = new WaitForEndOfFrame();

		float value = 0.0f;
		while(value < projStunDuration*data.stunDuration)
		{
			value += Time.deltaTime;
			float ev = data.pushCurve.Evaluate(value/(projStunDuration*data.stunDuration));
			iaMovement.Strafe(dir*Time.deltaTime*projPushBack*data.pushBack*ev);
			yield return wait;
		}
		stuned = false;
	}

	void Death()
	{
		if(dying == false)
		{
			StopAllCoroutines();
			StartCoroutine(DeathCoroutine());
		}
	}

	IEnumerator DeathCoroutine()
	{
		dying = true;
		SetUpComponents(false);
		iaActions = IAActions.wait;
		float force = damageable.pushBack*data.deathPushBack;
		rgdBody.AddForceAtPosition(damageable.damageDirection*force, damageable.damagePosition, ForceMode.VelocityChange);
		rgdBody.AddTorque(Vector3.Cross(damageable.damageDirection + Random.insideUnitSphere, Vector3.up)*-force, ForceMode.VelocityChange);

		GameObject deathParticle = ObjectPoolManager.instance.sphereBot_LVL0_Death.GetCurrentPooledGameObject();
		if(deathParticle)
		{
			deathParticle.SetActive(true);
			deathParticle.transform.position = trsf.position;
			deathParticle.transform.rotation = trsf.rotation;
			deathParticle.transform.SetParent(trsf);
		}
		else
		{
			Debug.Log("Neen More Chicken!");
		}

		yield return new WaitForSeconds(data.deathDuration);

		ParticleDeactivation[] p = GetComponentsInChildren<ParticleDeactivation>();
		for(int i = 0; i < p.Length; i++)
		{
			if(p[i].gameObject == deathParticle)
			{
				p[i].transform.SetParent(p[i].parent);
			}
			else
			{
				p[i].Disable();
			}
		}

		gameObject.SetActive(false);
	}

	void ResetVariables()
	{
		StopAllCoroutines();
		iaActions = IAActions.wait;
		lastKnownPosition = trsf.position;
		goingToSearchPosition = false;
		strafing = false;
		shoot = false;
		dying = false;
		stuned = false;
		SetUpComponents(true);
	}

	void SetUpComponents(bool b)
	{
		if(blind == false)
		{
			for(int i = 0; i < eyes.Length; i++)
			{
				eyes[i].enabled = b;
			}
		}
		for(int i = 0; i < iaWeapons.Length; i++)
		{
			iaWeapons[i].enabled = b;
		}
		iaMovement.navMeshAgent.enabled = b;
		iaMovement.enabled = b;
		damageable.enabled = b;
		iaBody.enabled = b;

		rgdBody.isKinematic = b;
		rgdBody.useGravity = !b;
	}

	void OnDisable()
	{
		ResetVariables();
	}
}