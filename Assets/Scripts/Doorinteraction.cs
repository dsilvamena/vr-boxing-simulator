using System.Collections;
using UnityEngine;

public class DoorInteration : MonoBehaviour
{
    [SerializeField] float openAngle = 90f;
    [SerializeField] float openSpeed = 2f;
    [SerializeField] float closeDelay = 2f;
    [SerializeField] bool isOpen = false;
    [SerializeField] string playerTag = "Player";

    private Quaternion _closedRotation;
    private Coroutine _rotationCoroutine;
    private Coroutine _closeDelayCoroutine;

    void Start()
    {
        _closedRotation = transform.rotation;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (_closeDelayCoroutine != null)
        {
            StopCoroutine(_closeDelayCoroutine);
            _closeDelayCoroutine = null;
        }

        if (!isOpen)
        {
            Vector3 dirToPlayer = (other.transform.position - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, dirToPlayer);
            float direction = dot > 0 ? 1f : -1f;

            Quaternion targetRotation = Quaternion.Euler(transform.eulerAngles + new Vector3(0, openAngle * direction, 0));
            StartRotation(targetRotation, true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (isOpen)
        {
            _closeDelayCoroutine = StartCoroutine(CloseAfterDelay());
        }
    }

    private IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(closeDelay);
        StartRotation(_closedRotation, false);
    }

    private void StartRotation(Quaternion targetRotation, bool openState)
    {
        isOpen = openState;
        if (_rotationCoroutine != null) StopCoroutine(_rotationCoroutine);
        _rotationCoroutine = StartCoroutine(RotateTo(targetRotation));
    }

    private IEnumerator RotateTo(Quaternion targetRotation)
    {
        while (Quaternion.Angle(transform.rotation, targetRotation) > 0.01f)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * openSpeed);
            yield return null;
        }
        transform.rotation = targetRotation;
    }
}