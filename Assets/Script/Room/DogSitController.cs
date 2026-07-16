using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class DogSitController : MonoBehaviour
{
	[SerializeField]
	private bool sitOnStart = true;

	[SerializeField]
	private string sitParameterName = "Sit_b";

	private Animator _animator;
	private int _sitParameterHash;

	private void Awake()
	{
		_animator = GetComponent<Animator>();
		_sitParameterHash = Animator.StringToHash(sitParameterName);
	}

	private void Start()
	{
		SetSitting(sitOnStart);
	}

	public void Sit()
	{
		SetSitting(true);
	}

	public void Stand()
	{
		SetSitting(false);
	}

	public void ToggleSitting()
	{
		SetSitting(!_animator.GetBool(_sitParameterHash));
	}

	public void SetSitting(bool isSitting)
	{
		if (!HasSitParameter())
		{
			Debug.LogWarning($"Animator 上找不到 Bool 参数“{sitParameterName}”。", this);
			return;
		}

		_animator.SetBool(_sitParameterHash, isSitting);
	}

	private bool HasSitParameter()
	{
		foreach (var parameter in _animator.parameters)
		{
			if (parameter.nameHash == _sitParameterHash && parameter.type == AnimatorControllerParameterType.Bool)
			{
				return true;
			}
		}

		return false;
	}
}
