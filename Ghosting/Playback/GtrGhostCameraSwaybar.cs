using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

[DefaultExecutionOrder(-50)]
public class GtrGhostCameraSwaybar : MonoBehaviour
{
    public Transform SoapboxRoot;
    public Transform MainCameraPosition;

    private float _upMult = 1.5f;

    private Vector3 _baseMainCameraPosition;
    private float _side;
    private float _up;

    private float _goToUpPosition;
    private float _smoothedVelocityUp;

    public GhostCameraSwaybar.SwayBarMode SwayMode;
    public bool LookBackwards;

    private void Start()
    {
        if (MainCameraPosition != null)
            _baseMainCameraPosition = MainCameraPosition.localPosition;

        _up = _upMult;
    }

    private void LateUpdate()
    {
        if (SoapboxRoot == null || MainCameraPosition == null)
            return;

        float deltaTime = Time.deltaTime;

        if (PlayerManager.Instance.instellingen.Settings.camera_smooth_delta_time &&
            PlayerManager.Instance.instellingen.Settings.vsync)
        {
            deltaTime = Time.smoothDeltaTime;
        }

        if (SwayMode == GhostCameraSwaybar.SwayBarMode.moveStatic)
        {
            if (!LookBackwards)
            {
                transform.localPosition = Vector3.zero;
                transform.localEulerAngles = Vector3.zero;
            }
            else
            {
                transform.localPosition = Vector3.zero;
                transform.localEulerAngles = new Vector3(0, 180, 0);
            }

            MainCameraPosition.localPosition = _baseMainCameraPosition;
        }
        else if (SwayMode == GhostCameraSwaybar.SwayBarMode.moveDynamic)
        {
            transform.localPosition = Vector3.zero;

            if (!LookBackwards)
                transform.LookAt(transform.position + SoapboxRoot.forward);
            else
                transform.LookAt(transform.position - SoapboxRoot.forward);

            Vector3 fwd = SoapboxRoot.forward;
            fwd.y = 0;

            float sign = LookBackwards ? -1f : 1f;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            float roll = Vector3.Angle(right, SoapboxRoot.right) *
                         Mathf.Sign(SoapboxRoot.right.y) *
                         Mathf.Deg2Rad;

            _side = Mathf.Lerp(_side, SoapboxRoot.right.y * sign, 0.2f * (60 * deltaTime));
            _up = Mathf.Lerp(_up, Mathf.Cos(roll) * _upMult, 0.05f * (60 * deltaTime));

            _goToUpPosition = _goToUpPosition * Mathf.Cos(roll);
            _smoothedVelocityUp = Mathf.Lerp(_smoothedVelocityUp, _goToUpPosition, 0.05f * 60 * Time.deltaTime);

            MainCameraPosition.localPosition = _baseMainCameraPosition +
                                               new Vector3(-_side * 0.74f, (_up - _upMult) + _smoothedVelocityUp, 0);
        }
        else if (SwayMode == GhostCameraSwaybar.SwayBarMode.moveOrbital)
        {
            transform.localPosition = Vector3.zero;
            transform.eulerAngles = Vector3.zero;
        }
    }
}
