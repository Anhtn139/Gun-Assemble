using UnityEngine;
using System.Collections;
using MoreMountains.Tools;

namespace MoreMountains.TopDownEngine
{
	/// <summary>
	/// A class to add on a CharacterPathfinder3D equipped character.
	/// It will allow you to click anywhere on screen, which will determine a new target and the character will pathfind its way to it
	/// </summary>
	[AddComponentMenu("TopDown Engine/Character/AI/Automation/Mouse Driven Pathfinder AI 3D")]
	public class MouseDrivenPathfinderAI3D : TopDownMonoBehaviour 
	{
		[Header("Testing")]
		/// the camera we'll use to determine the destination from
		[Tooltip("the camera we'll use to determine the destination from")]
		public Camera Cam;
		/// a gameobject used to show the destination
		[Tooltip("a gameobject used to show the destination")]
		public GameObject Destination;

		[Header("Pathfinding Optimization")]
		[Tooltip("Khoảng dịch chuyển tối thiểu của Destination để trigger cập nhật đường (m)")]
		public float DestinationMoveThreshold = 0.25f;
		[Tooltip("Nếu Destination null, script sẽ cố gắng tìm GameObject tag 'Player' mỗi khoảng thời gian này (s)")]
		public float TryFindPlayerInterval = 1f;

		protected CharacterPathfinder3D _characterPathfinder3D;
		protected Plane _playerPlane;
		protected bool _destinationSet = false;
		protected Camera _mainCamera;

		protected Coroutine _updateCoroutine = null;
		protected Transform _lastDestinationTransform = null;
		protected Vector3 _lastDestinationPosition = Vector3.positiveInfinity;

		/// <summary>
		/// On awake we create a plane to catch our ray
		/// </summary>
		protected virtual void Awake()
		{
			_mainCamera = Camera.main;
			_characterPathfinder3D = this.gameObject.GetComponent<CharacterPathfinder3D>();
			_playerPlane = new Plane(Vector3.up, Vector3.zero);
		}

		/// <summary>
		 /// Start the update coroutine once.
		 /// </summary>
		protected virtual void Start()
		{
			if (Destination == null)
			{
				Destination = GameObject.FindWithTag("Player");
			}
			if (_updateCoroutine == null)
			{
				_updateCoroutine = StartCoroutine(UpdatePathRoutine());
			}
		}

		protected virtual void OnDisable()
		{
			if (_updateCoroutine != null)
			{
				StopCoroutine(_updateCoroutine);
				_updateCoroutine = null;
			}
		}

		/// <summary>
		/// On Update we left DetectMouse available but we don't start coroutines each frame.
		/// </summary>
		protected virtual void Update()
		{
			/* DetectMouse(); */
			// nothing else here to avoid starting coroutines every frame
		}

		/// <summary>
		/// If the mouse is clicked, we cast a ray and if that ray hits the plane we make it the pathfinding target
		/// </summary>
		protected virtual void DetectMouse()
		{
			if (Input.GetMouseButtonDown(0))
			{
				Ray ray = _mainCamera.ScreenPointToRay(InputManager.Instance.MousePosition);
				Debug.DrawRay(ray.origin, ray.direction * 100, Color.yellow);
				float distance;
				if (_playerPlane.Raycast(ray, out distance))
				{
					Vector3 target = ray.GetPoint(distance);
					Destination.transform.position = target;
					_destinationSet = true; 
					_characterPathfinder3D.SetNewDestination(Destination.transform);
				}
			}
		}
		
		/// <summary>
		/// Centralized coroutine that updates the path intelligently:
		/// - starts once
		/// - uses CharacterPathfinder3D.RefreshInterval and MinimumDelayBeforePollingNavmesh to pace requests
		/// - only calls SetNewDestination when destination changed significantly (or when mode requires it)
		/// </summary>
		IEnumerator UpdatePathRoutine()
		{
			// safety
			if (_characterPathfinder3D == null)
			{
				yield break;
			}

			// ensure sensible defaults
			float minDelay = Mathf.Max(0.01f, _characterPathfinder3D.MinimumDelayBeforePollingNavmesh);
			float refreshInterval = Mathf.Max(minDelay, _characterPathfinder3D.RefreshInterval);

			while (true)
			{
				if (Destination == null)
				{
					// try to find player occasionally
					Destination = GameObject.FindWithTag("Player");
					yield return new WaitForSeconds(TryFindPlayerInterval);
					continue;
				}

				Transform destT = Destination.transform;
				Vector3 destPos = destT.position;

				bool shouldUpdate = false;

				// If PathRefreshMode is None, update only when target changed (or first time)
				if (_characterPathfinder3D.PathRefreshMode == CharacterPathfinder3D.PathRefreshModes.None)
				{
					if (_lastDestinationTransform != destT) shouldUpdate = true;
					else if ((_lastDestinationPosition - destPos).sqrMagnitude > DestinationMoveThreshold * DestinationMoveThreshold) shouldUpdate = true;
				}
				// If TimeBased, always update every RefreshInterval
				else if (_characterPathfinder3D.PathRefreshMode == CharacterPathfinder3D.PathRefreshModes.TimeBased)
				{
					shouldUpdate = true;
				}
				// If SpeedThresholdBased, we still update periodically but only if the agent seems stuck will the pathfinder re-route more aggressively.
				else if (_characterPathfinder3D.PathRefreshMode == CharacterPathfinder3D.PathRefreshModes.SpeedThresholdBased)
				{
					// We don't have direct speed here; trigger periodic updates, but we avoid spamming by respecting MinimumDelayBeforePollingNavmesh.
					shouldUpdate = true;
				}
				else
				{
					shouldUpdate = true;
				}

				if (shouldUpdate)
				{
					_characterPathfinder3D.SetNewDestination(destT);
					_lastDestinationTransform = destT;
					_lastDestinationPosition = destPos;
				}

				// use character's refresh interval but ensure it's at least the minimum delay
				refreshInterval = Mathf.Max(minDelay, _characterPathfinder3D.RefreshInterval);
				yield return new WaitForSeconds(refreshInterval);
			}
		}
	}
}