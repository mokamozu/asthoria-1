using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>Estojo ejetado: cai girando e some.</summary>
    public sealed class FallingCasing : MonoBehaviour
    {
        private Vector3 _v, _spin;
        private float _life;

        private void Start()
        {
            _v = transform.TransformDirection(new Vector3(-0.6f, 0.4f, 0f)) + Random.insideUnitSphere * 0.4f;
            _spin = Random.insideUnitSphere * 720f;
        }

        private void Update()
        {
            _v += Physics.gravity * Time.deltaTime;
            transform.position += _v * Time.deltaTime;
            transform.Rotate(_spin * Time.deltaTime, Space.Self);
            _life += Time.deltaTime;
            if (_life > 1.2f) Destroy(gameObject);
        }
    }
}
