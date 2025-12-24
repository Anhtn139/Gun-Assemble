using System;
using UnityEngine;
using System.Collections;
using MoreMountains.Tools;

public class ApplyProjectileChange : ASignal<float> {}

namespace MoreMountains.TopDownEngine
{	
	/// <summary>
	/// Projectile class to be used along with projectile weapons
	/// </summary>
	[AddComponentMenu("TopDown Engine/Weapons/Projectile")]
	public class Projectile : MMPoolableObject  
	{
		public enum MovementVectors { Forward, Right, Up}
		
		[Header("Movement")]
		/// if true, the projectile will rotate at initialization towards its rotation
		[Tooltip("if true, the projectile will rotate at initialization towards its rotation")]
		public bool FaceDirection = true;
		/// if true, the projectile will rotate towards movement
		[Tooltip("if true, the projectile will rotate towards movement")]
		public bool FaceMovement = false;
		/// if FaceMovement is true, the projectile's vector specified below will be aligned to the movement vector, usually you'll want to go with Forward in 3D, Right in 2D
		[Tooltip("if FaceMovement is true, the projectile's vector specified below will be aligned to the movement vector, usually you'll want to go with Forward in 3D, Right in 2D")]
		[MMCondition("FaceMovement", true)]
		public MovementVectors MovementVector = MovementVectors.Forward;

		/// the speed of the object (relative to the level's speed)
		[Tooltip("the speed of the object (relative to the level's speed)")]
		public float Speed = 0;
		/// the acceleration of the object over time. Starts accelerating on enable.
		[Tooltip("the acceleration of the object over time. Starts accelerating on enable.")]
		public float Acceleration = 0;
		/// the current direction of the object
		[Tooltip("the current direction of the object")]
		public Vector3 Direction = Vector3.left;
		/// if set to true, the spawner can change the direction of the object. If not the one set in its inspector will be used.
		[Tooltip("if set to true, the spawner can change the direction of the object. If not the one set in its inspector will be used.")]
		public bool DirectionCanBeChangedBySpawner = true;
		/// the flip factor to apply if and when the projectile is mirrored
		[Tooltip("the flip factor to apply if and when the projectile is mirrored")]
		public Vector3 FlipValue = new Vector3(-1,1,1);
		/// set this to true if your projectile's model (or sprite) is facing right, false otherwise
		[Tooltip("set this to true if your projectile's model (or sprite) is facing right, false otherwise")]
		public bool ProjectileIsFacingRight = true;

		[Header("Spawn")]
		[MMInformation("Here you can define an initial delay (in seconds) during which this object won't take or cause damage. This delay starts when the object gets enabled. You can also define whether the projectiles should damage their owner (think rockets and the likes) or not",MoreMountains.Tools.MMInformationAttribute.InformationType.Info,false)]
		/// the initial delay during which the projectile can't be destroyed
		[Tooltip("the initial delay during which the projectile can't be destroyed")]
		public float InitialInvulnerabilityDuration=0f;
		/// should the projectile damage its owner?
		[Tooltip("should the projectile damage its owner?")]
		public bool DamageOwner = false;

		[Header("Explosion (optional)")]
		[Tooltip("If true, the projectile will apply area damage on death.")]
		public bool ExplodeOnDeath = false;
		[Tooltip("Explosion radius (world units).")]
		public float ExplosionRadius = 2f;
		[Tooltip("Min damage applied per target in explosion.")]
		public float ExplosionMinDamage = 10f;
		[Tooltip("Max damage applied per target in explosion.")]
		public float ExplosionMaxDamage = 10f;
		[Tooltip("Which layers can be damaged by the explosion.")]
		public LayerMask ExplosionLayerMask;
		[Tooltip("Invincibility duration to pass to damaged Health (after being hit by explosion).")]
		public float ExplosionInvincibilityDuration = 0.25f;

		[Header("Chaining (optional)")]
		[Tooltip("Number of times this projectile can chain to another target after hitting one (0 = no chaining).")]
		public int ChainCount = 2;
		[Tooltip("Max distance (world units) to search for the next chain target.")]
		public float ChainRange = 5f;

		/// Returns the associated damage on touch zone
		public virtual DamageOnTouch TargetDamageOnTouch { get { return _damageOnTouch; } }
		public virtual Weapon SourceWeapon { get { return _weapon; } }

		protected Weapon _weapon;
		protected GameObject _owner;
		protected Vector3 _movement;
		protected float _initialSpeed;
		protected SpriteRenderer _spriteRenderer;
		protected DamageOnTouch _damageOnTouch;
		protected WaitForSeconds _initialInvulnerabilityDurationWFS;
		protected Collider _collider;
		protected Collider2D _collider2D;
		protected Rigidbody _rigidBody;
		protected Rigidbody2D _rigidBody2D;
		protected bool _facingRightInitially;
		protected bool _initialFlipX;
		protected Vector3 _initialLocalScale;
		protected bool _shouldMove = true;
		protected Health _health;
		protected bool _spawnerIsFacingRight;

		// chaining runtime
		protected int _remainingChains = 0;

		// protection flags to avoid this projectile being pooled/destroyed during a chain event
		protected bool _protectFromDestroyThisFrame = false;
		protected bool _deferredDestroyRequested = false;

		// prevents multiple chain spawns from the same collision (e.g. multiple callbacks same frame)
		protected bool _chainingInProgress = false;

		/// <summary>
		 /// Expose whether this projectile currently can chain (runtime, does not modify serialized ChainCount).
		 /// </summary>
		public bool IsChainingActive { get { return _remainingChains > 0; } }

		/// <summary>
		/// On awake, we store the initial speed of the object 
		/// </summary>
		protected virtual void Awake ()
		{
			_facingRightInitially = ProjectileIsFacingRight;
			_initialSpeed = Speed;
			_health = GetComponent<Health> ();
			_collider = GetComponent<Collider> ();
			_collider2D = GetComponent<Collider2D>();
			_spriteRenderer = GetComponent<SpriteRenderer> ();
			_damageOnTouch = GetComponent<DamageOnTouch>();
			_rigidBody = GetComponent<Rigidbody> ();
			_rigidBody2D = GetComponent<Rigidbody2D> ();
			_initialInvulnerabilityDurationWFS = new WaitForSeconds (InitialInvulnerabilityDuration);
			if (_sprite_renderer_not_null()) {	_initialFlipX = _spriteRenderer.flipX ;		}
			_initialLocalScale = transform.localScale;
			
		}

		/// <summary>
		/// Handles the projectile's initial invincibility
		/// </summary>
		/// <returns>The invulnerability.</returns>
		protected virtual IEnumerator InitialInvulnerability()
		{
			if (_damageOnTouch == null) { yield break; }
			if (_weapon == null) { yield break; }

			_damageOnTouch.ClearIgnoreList();
			if (_weapon.Owner != null)
			{
				_damageOnTouch.IgnoreGameObject(_weapon.Owner.gameObject);	
			}
			yield return _initialInvulnerabilityDurationWFS;
			if (DamageOwner)
			{
				_damageOnTouch.StopIgnoringObject(_weapon.Owner.gameObject);
			}
		}

		/// <summary>
		/// Initializes the projectile
		/// </summary>
		protected virtual void Initialization()
		{
			Speed = _initialSpeed;
			ProjectileIsFacingRight = _facingRightInitially;
			if (_sprite_renderer_not_null()) {	_spriteRenderer.flipX = _initialFlipX;	}
			transform.localScale = _initialLocalScale;	
			_shouldMove = true;
			_damageOnTouch?.InitializeFeedbacks();

			if (_collider != null)
			{
				_collider.enabled = true;
			}
			if (_collider2D != null)
			{
				_collider2D.enabled = true;
			}

			// init chaining
			_remainingChains = ChainCount;
			_chainingInProgress = false;
		}

		// helper to avoid analyzer warnings about sprite renderer usage
		private bool _sprite_renderer_not_null() { return _spriteRenderer != null; }

		/// <summary>
		/// On update(), we move the object based on the level's speed and the object's speed, and apply acceleration
		/// </summary>
		protected virtual void FixedUpdate ()
		{
			base.Update ();
			if (_shouldMove)
			{
				Movement();
			}
		}

		/// <summary>
		/// Handles the projectile's movement, every frame
		/// </summary>
		public virtual void Movement()
		{
			_movement = Direction * (Speed / 10) * Time.deltaTime;
			//transform.Translate(_movement,Space.World);
			if (_rigidBody != null)
			{
				_rigidBody.MovePosition (this.transform.position + _movement);
			}
			if (_rigidBody2D != null)
			{
				_rigidBody2D.MovePosition(this.transform.position + _movement);
			}
			// We apply the acceleration to increase the speed
			Speed += Acceleration * Time.deltaTime;
		}

		/// <summary>
		/// Sets the projectile's direction.
		/// </summary>
		/// <param name="newDirection">New direction.</param>
		/// <param name="newRotation">New rotation.</param>
		/// <param name="spawnerIsFacingRight">If set to <c>true</c> spawner is facing right.</param>
		public virtual void SetDirection(Vector3 newDirection, Quaternion newRotation, bool spawnerIsFacingRight = true)
		{
			_spawnerIsFacingRight = spawnerIsFacingRight;

			if (DirectionCanBeChangedBySpawner)
			{
				Direction = newDirection;
			}
			if (ProjectileIsFacingRight != spawnerIsFacingRight)
			{
				Flip ();
			}
			if (FaceDirection)
			{
				transform.rotation = newRotation;
			}

			if (_damageOnTouch != null)
			{
				_damageOnTouch.SetKnockbackScriptDirection(newDirection);
			}

			if (FaceMovement)
			{
				switch (MovementVector)
				{
					case MovementVectors.Forward:
						transform.forward = newDirection;
						break;
					case MovementVectors.Right:
						transform.right = newDirection;
						break;
					case MovementVectors.Up:
						transform.up = newDirection;
						break;
				}
			}
		}

		/// <summary>
		/// Flip the projectile
		/// </summary>
		protected virtual void Flip()
		{
			if (_spriteRenderer != null)
			{
				_spriteRenderer.flipX = !_spriteRenderer.flipX;
			}	
			else
			{
				this.transform.localScale = Vector3.Scale(this.transform.localScale,FlipValue) ;
			}
		}
        
		/// <summary>
		/// Flip the projectile
		/// </summary>
		protected virtual void Flip(bool state)
		{
			if (_sprite_renderer_not_null())
			{
				_spriteRenderer.flipX = state;
			}
			else
			{
				this.transform.localScale = Vector3.Scale(this.transform.localScale, FlipValue);
			}
		}

		/// <summary>
		/// Sets the projectile's parent weapon.
		/// </summary>
		/// <param name="newWeapon">New weapon.</param>
		public virtual void SetWeapon(Weapon newWeapon)
		{
			_weapon = newWeapon;
		}

		/// <summary>
		 /// Runtime-only setter for remaining chain count (does not modify serialized ChainCount)
		 /// </summary>
		/// <param name="count"></param>
		public virtual void SetRemainingChains(int count)
		{
			_remainingChains = Mathf.Max(0, count);
		}

		/// <summary>
		 /// Override Destroy to defer pooling when we're protecting this instance during a chain event.
		 /// </summary>
		public override void Destroy()
		{
			// If we're protected for the current frame (chain in progress), defer destruction.
			if (_protectFromDestroyThisFrame)
			{
				_deferredDestroyRequested = true;
				return;
			}

			base.Destroy();
		}

		/// <summary>
		/// Small coroutine to protect this instance from being pooled/destroyed during the current frame.
		/// If a destroy was requested while protected, it will be executed after protection ends.
		/// </summary>
		protected virtual IEnumerator ProtectFromDestroyOneFrame()
		{
			_protectFromDestroyThisFrame = true;
			yield return null;
			_protectFromDestroyThisFrame = false;
			if (_deferredDestroyRequested)
			{
				_deferredDestroyRequested = false;
				base.Destroy();
			}
		}

		/// <summary>
		 /// Small coroutine to reset chaining guard after one frame.
		 /// Prevents multiple chain spawns from several collision callbacks in the same frame.
		 /// </summary>
		protected virtual IEnumerator ResetChainingGuard()
		{
			yield return null;
			_chainingInProgress = false;
		}

		/// <summary>
		 /// Deactivate (return to pool) after one frame to let damage callbacks finish.
		 /// </summary>
		protected virtual IEnumerator DeactivateAfterFrame()
		{
			yield return null;
			StopAt();
			base.Destroy();
		}

		/// <summary>
		/// Sets the damage caused by the projectile's DamageOnTouch to the specified value
		/// </summary>
		/// <param name="newDamage"></param>
		public virtual void SetDamage(float minDamage, float maxDamage)
		{
			if (_damageOnTouch != null)
			{
				_damageOnTouch.MinDamageCaused = minDamage;
				_damageOnTouch.MaxDamageCaused = maxDamage; 
			}
		}

		/// <summary>
		/// Applies area damage (3D and 2D). Finds Health components in the radius and calls Damage(...) on them.
		/// </summary>
		public virtual void ApplyAreaDamage(Vector3 center, float radius, float minDamage, float maxDamage, LayerMask layerMask, GameObject instigator)
		{
			// 3D overlap
			Collider[] cols = Physics.OverlapSphere(center, radius, layerMask);
			if (cols != null)
			{
				foreach (var col in cols)
				{
					if (col == null) continue;
					var h = col.gameObject.MMGetComponentNoAlloc<Health>() ?? col.GetComponentInParent<Health>();
					if (h == null) continue;
					if (!h.CanTakeDamageThisFrame()) continue;
					float dmg = UnityEngine.Random.Range(minDamage, Mathf.Max(maxDamage, minDamage));
					Vector3 dir = (h.transform.position - center).normalized;
					h.Damage(dmg, instigator, 0f, ExplosionInvincibilityDuration, dir, null);
				}
			}

			// 2D overlap (if project uses 2D colliders)
			Collider2D[] cols2 = Physics2D.OverlapCircleAll(new Vector2(center.x, center.y), radius, layerMask);
			if (cols2 != null)
			{
				foreach (var col in cols2)
				{
					if (col == null) continue;
					var h = col.gameObject.MMGetComponentNoAlloc<Health>() ?? col.GetComponentInParent<Health>();
					if (h == null) continue;
					if (!h.CanTakeDamageThisFrame()) continue;
					float dmg = UnityEngine.Random.Range(minDamage, Mathf.Max(maxDamage, minDamage));
					Vector3 dir = (h.transform.position - center).normalized;
					h.Damage(dmg, instigator, 0f, ExplosionInvincibilityDuration, dir, null);
				}
			}
		}
        
		/// <summary>
		/// Sets the projectile's owner.
		/// </summary>
		/// <param name="newOwner">New owner.</param>
		public virtual void SetOwner(GameObject newOwner)
		{
			_owner = newOwner;
			DamageOnTouch damageOnTouch = this.gameObject.MMGetComponentNoAlloc<DamageOnTouch>();
			if (damageOnTouch != null)
			{
				damageOnTouch.Owner = newOwner;
				if (!DamageOwner)
				{
					damageOnTouch.ClearIgnoreList();
					damageOnTouch.IgnoreGameObject(newOwner);
				}
			}
		}

		/// <summary>
		/// Returns the current Owner of the projectile
		/// </summary>
		/// <returns></returns>
		public virtual GameObject GetOwner()
		{
			return _owner;
		}

		/// <summary>
		/// On death, disables colliders and prevents movement
		/// </summary>
		public virtual void StopAt()
		{
			if (_collider != null)
			{
				_collider.enabled = false;
			}
			if (_collider2D != null)
			{
				_collider2D.enabled = false;
			}
			
			_shouldMove = false;
		}

		/// <summary>
		/// On death, we stop our projectile
		/// </summary>
		protected virtual void OnDeath()
		{
			StopAt ();
		}

		void ApplyChanges(float f)
		{
			float dame = _damageOnTouch.MaxDamageCaused * f;
			SetDamage(dame, dame);
		}
		
		/// <summary>
		/// On enable, we trigger a short invulnerability
		/// </summary>
		protected override void OnEnable ()
		{
			base.OnEnable ();

			Initialization();
			if (InitialInvulnerabilityDuration>0)
			{
				StartCoroutine(InitialInvulnerability());
			}

			if (_health != null)
			{
				_health.OnDeath += OnDeath;
			}
			
			Signals.Get<ApplyProjectileChange>().AddListener(ApplyChanges);
		}

		/// <summary>
		/// On disable, we plug our OnDeath method to the health component
		/// </summary>
		protected override void OnDisable()
		{
			base.OnDisable ();
			if (_health != null)
			{
				_health.OnDeath -= OnDeath;
			}

			// Reset chaining state and damage ignore list when returned to pool
			_remainingChains = ChainCount;
			_damageOnTouch?.ClearIgnoreList();
			// reset protection flags
			_protectFromDestroyThisFrame = false;
			_deferredDestroyRequested = false;
			_chainingInProgress = false;
			Signals.Get<ApplyProjectileChange>().RemoveListener(ApplyChanges);
		}

		#region Collision handling for chaining
		// Handle both 3D and 2D collisions/triggers so we can detect hits and attempt to chain

		protected virtual void OnCollisionEnter(Collision collision)
		{
			if (!_shouldMove) { return; }
			HandleCollisionWith(collision.gameObject);
		}

		protected virtual void OnTriggerEnter(Collider other)
		{
			if (!_shouldMove) { return; }
			HandleCollisionWith(other.gameObject);
		}

		protected virtual void OnCollisionEnter2D(UnityEngine.Collision2D collision)
		{
			if (!_shouldMove) { return; }
			HandleCollisionWith(collision.gameObject);
		}

		protected virtual void OnTriggerEnter2D(Collider2D other)
		{
			if (!_shouldMove) { return; }
			HandleCollisionWith(other.gameObject);
		}

		/// <summary>
		/// Common collision handler: if we hit a damageable target, try to chain
		/// </summary>
		/// <param name="other"></param>
		protected virtual void HandleCollisionWith(GameObject other)
		{
			if (other == null) { return; }
			// ignore owner
			if (_owner != null && other == _owner) { return; }

			// require a Health on the other to consider it a "hit"
			var hitHealth = other.MMGetComponentNoAlloc<Health>() ?? other.GetComponentInParent<Health>();
			if (hitHealth == null) { return; }

			// Try to determine whether the target can be damaged now.
			// If CanTakeDamageThisFrame() returns false because the target is dead (CurrentHealth <= 0),
			// we skip chaining. If it's false due to invulnerability/temporary immunity we still allow chaining
			// because DamageOnTouch may have run before this callback and put the target into invulnerable state.
			bool canTakeDamage = true;
			try
			{
				canTakeDamage = hitHealth.CanTakeDamageThisFrame();
			}
			catch
			{
#if UNITY_EDITOR
				Debug.LogWarning($"Projectile.HandleCollisionWith: CanTakeDamageThisFrame threw for {other.name}, assuming damageable.", this);
#endif
				canTakeDamage = true;
			}

			if (!canTakeDamage)
			{
				// If target is actually dead (health <= 0 and InitialHealth != 0), don't chain
				if (hitHealth.CurrentHealth <= 0 && hitHealth.InitialHealth != 0)
				{
#if UNITY_EDITOR
					Debug.Log($"Projectile: target {other.name} appears dead (CurrentHealth <= 0), skipping chain.", this);
#endif
					return;
				}
#if UNITY_EDITOR
				Debug.Log($"Projectile: target {other.name} cannot take damage this frame (invulnerable/immune) but chaining will still be attempted.", this);
#endif
			}

#if UNITY_EDITOR
			Debug.Log($"Projectile.HandleCollisionWith hit:{other.name} remainingChains:{_remainingChains} pos:{transform.position}", this);
#endif

			 // prevent multiple chain redirects in same frame
			if (_chainingInProgress)
			{
#if UNITY_EDITOR
				Debug.Log("Projectile: chaining already in progress this frame, ignoring duplicate collision.", this);
#endif
				return;
			}

			// if no chaining configured, nothing more to do here
			if (_remainingChains <= 0)
			{
#if UNITY_EDITOR
				Debug.Log("Projectile: no remaining chains, skipping chain logic.", this);
#endif
				// still allow damage to be applied by DamageOnTouch; we won't redirect
				return;
			}

			// attempt to find next target
			GameObject next = FindClosestValidTarget(transform.position, other);
			if (next == null)
			{
#if UNITY_EDITOR
				Debug.Log($"Projectile: FindClosestValidTarget returned null. ChainRange:{ChainRange}", this);
#endif
				// no target found -> do nothing (no chain)
				return;
			}

			// mark guard to avoid duplicates, reset after one frame
			_chainingInProgress = true;
			StartCoroutine(ResetChainingGuard());

#if UNITY_EDITOR
			Debug.Log($"Projectile: chaining redirect from {other.name} to {next.name}", this);
#endif

			// protect this instance from being destroyed/pooled during the current frame
			StartCoroutine(ProtectFromDestroyOneFrame());

			// delay ignore so the current target still receives damage this frame
			StartCoroutine(DelayIgnoreThenKeep(other));

			// Redirect this same projectile toward next target (preserve Y)
			Vector3 targetPos = next.transform.position;
			targetPos.y = this.transform.position.y;
			Vector3 newDir = (targetPos - this.transform.position).normalized;
			if (newDir == Vector3.zero)
			{
				newDir = this.Direction != Vector3.zero ? this.Direction : Vector3.forward;
			}

			Direction = newDir;
			_damageOnTouch?.SetKnockbackScriptDirection(newDir);

			// optionally align visuals
			if (FaceMovement)
			{
				switch (MovementVector)
				{
					case MovementVectors.Forward:
						transform.forward = newDir;
						break;
					case MovementVectors.Right:
						transform.right = newDir;
						break;
					case MovementVectors.Up:
						transform.up = newDir;
						break;
				}
			}

			// decrease remaining chains; if none left, deactivate after one frame
			_remainingChains--;
			if (_remainingChains <= 0)
			{
				StartCoroutine(DeactivateAfterFrame());
			}
		}

		/// <summary>
		/// Coroutine: wait one physics tick before adding the target to the ignore list,
		/// so the target still receives damage from the initial collision.
		/// </summary>
		/// <param name="go"></param>
		protected virtual IEnumerator DelayIgnoreThenKeep(GameObject go)
		{
			// Wait one frame to allow collision/damage processing to complete.
			yield return null;
			_damageOnTouch?.IgnoreGameObject(go);
		}

		/// <summary>
		/// Finds the closest valid target for chaining within ChainRange, excluding 'exclude' and owner.
		/// Uses DamageOnTouch.TargetLayerMask when available.
		 /// Only candidates with tag "Enemy" are considered.
		/// </summary>
		/// <param name="center"></param>
		/// <param name="exclude"></param>
		/// <returns></returns>
		protected virtual GameObject FindClosestValidTarget(Vector3 center, GameObject exclude)
		{
			LayerMask mask = (_damageOnTouch != null) ? _damageOnTouch.TargetLayerMask : ~0;

			GameObject best = null;
			float bestDist = float.MaxValue;

			// 3D search
			Collider[] cols = Physics.OverlapSphere(center, ChainRange, mask);
			if (cols != null)
			{
				foreach (var col in cols)
				{
					if (col == null) continue;
					var candidate = col.gameObject;
					if (candidate == exclude) continue;
					if (_owner != null && candidate == _owner) continue;

					// Only chain to objects tagged "Enemy"
					if (!candidate.CompareTag("Enemy")) continue;

					var h = candidate.MMGetComponentNoAlloc<Health>() ?? candidate.GetComponentInParent<Health>();
					if (h == null) continue;
					try
					{
						if (!h.CanTakeDamageThisFrame()) continue;
					}
					catch { }

					float d = Vector3.Distance(center, candidate.transform.position);
					if (d < bestDist)
					{
						bestDist = d;
						best = candidate;
					}
				}
			}

			// 2D search if nothing found or just to consider 2D colliders as well
			Collider2D[] cols2 = Physics2D.OverlapCircleAll(new Vector2(center.x, center.y), ChainRange, mask);
			if (cols2 != null)
			{
				foreach (var col in cols2)
				{
					if (col == null) continue;
					var candidate = col.gameObject;
					if (candidate == exclude) continue;
					if (_owner != null && candidate == _owner) continue;

					// Only chain to objects tagged "Enemy"
					if (!candidate.CompareTag("Enemy")) continue;

					var h = candidate.MMGetComponentNoAlloc<Health>() ?? candidate.GetComponentInParent<Health>();
					if (h == null) continue;
					try
					{
						if (!h.CanTakeDamageThisFrame()) continue;
					}
					catch { }

					float d = Vector3.Distance(center, candidate.transform.position);
					if (d < bestDist)
					{
						bestDist = d;
						best = candidate;
					}
				}
			}

			return best;
		}
		#endregion
	}	
}