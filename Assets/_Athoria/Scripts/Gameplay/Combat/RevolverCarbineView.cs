using System.Collections;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Animações da Carabina de Tambor da Evelyn em primeira pessoa:
    /// tiro (cão, clarão, tambor gira 60°), recarga (tambor abre para o lado,
    /// ejeta os estojos, entram 6 balas .38 uma a uma, fecha seco) e inspeção (V).
    /// Fica no Prefab da arma; escuta o FirearmAmmo do jogador.
    /// </summary>
    public sealed class RevolverCarbineView : MonoBehaviour
    {
        private enum Chamber { Empty, Live, Spent }

        [Header("Peças")]
        [SerializeField] private Transform model;            // gira inteiro na inspeção (a raiz da arma, presa na mão)
        [SerializeField] private Transform hammer;           // gira em Z para armar
        [SerializeField] private Transform crane;            // braço do tambor: gira em X para abrir
        [SerializeField] private Transform cylinder;         // gira em X de 60 em 60 graus
        [SerializeField] private GameObject[] rounds = new GameObject[6];
        [SerializeField] private GameObject[] bullets = new GameObject[6];   // projétil de cada bala (some no estojo vazio)
        [SerializeField] private Renderer[] cases = new Renderer[6];
        [SerializeField] private Material liveCase, spentCase;
        [SerializeField] private GameObject flash;
        [SerializeField] private Light flashLight;

        [Header("Ajustes")]
        [SerializeField, Tooltip("Alavanca em anel: abre e fecha depois de cada tiro (opcional).")] private Transform lever;
        [SerializeField] private float leverAngle = 40f;
        [SerializeField] private float openAngle = 80f;
        [SerializeField] private float hammerCock = 38f;
        [SerializeField] private AudioClip clickSound, openSound, closeSound, roundSound;

        private FirearmAmmo _ammo;
        private readonly Chamber[] _state = new Chamber[6];
        private Vector3[] _roundHome;
        private int _top, _turns;
        private Coroutine _busy;
        private Quaternion _modelRest;

        private void Awake()
        {
            _roundHome = new Vector3[rounds.Length];
            for (var i = 0; i < rounds.Length; i++) if (rounds[i] != null) _roundHome[i] = rounds[i].transform.localPosition;
            if (flash != null) flash.SetActive(false);
            if (flashLight != null) flashLight.enabled = false;
        }

        private void OnEnable()
        {
            _ammo = GetComponentInParent<FirearmAmmo>();
            if (_ammo == null) return;
            _ammo.Fired += OnFired;
            _ammo.DryFired += OnDry;
            _ammo.ReloadStarted += OnReload;
            _ammo.ReloadCancelled += OnCancel;
            _ammo.InspectRequested += OnInspect;
            // tambor com o que sobrou no carregador
            var n = _ammo.CurrentRounds;
            for (var i = 0; i < 6; i++) _state[(_top + i) % 6] = i < n ? Chamber.Live : Chamber.Spent;
            Refresh();
        }

        private void OnDisable()
        {
            if (_ammo == null) return;
            _ammo.Fired -= OnFired;
            _ammo.DryFired -= OnDry;
            _ammo.ReloadStarted -= OnReload;
            _ammo.ReloadCancelled -= OnCancel;
            _ammo.InspectRequested -= OnInspect;
        }

        private void Refresh()
        {
            for (var i = 0; i < rounds.Length && i < 6; i++)
            {
                if (rounds[i] == null) continue;
                rounds[i].SetActive(_state[i] != Chamber.Empty);
                rounds[i].transform.localPosition = _roundHome[i];
                if (bullets[i] != null) bullets[i].SetActive(_state[i] == Chamber.Live);
                if (cases[i] != null) cases[i].sharedMaterial = _state[i] == Chamber.Spent ? spentCase : liveCase;
            }
        }

        private void Play(IEnumerator routine)
        {
            if (_busy != null) StopCoroutine(_busy);
            _busy = StartCoroutine(Wrap(routine));
        }

        private IEnumerator Wrap(IEnumerator routine)
        {
            yield return routine;
            _busy = null;
        }

        private void Sound(AudioClip clip, float volume = 0.7f)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, volume);
        }

        private static IEnumerator Tween(float time, System.Action<float> step)
        {
            for (var t = 0f; t < time; t += Time.deltaTime)
            {
                step(Mathf.SmoothStep(0f, 1f, t / time));
                yield return null;
            }
            step(1f);
        }

        // ---------- Tiro ----------
        private void OnFired()
        {
            _state[_top] = Chamber.Spent;
            Play(FireRoutine());
        }

        private IEnumerator FireRoutine()
        {
            SetCrane(0f);
            hammer.localRotation = Quaternion.Euler(0f, 0f, hammerCock);
            Refresh();
            if (flash != null)
            {
                flash.SetActive(true);
                flash.transform.localRotation = Quaternion.Euler(Random.Range(0f, 360f), 0f, 0f);
            }
            if (flashLight != null) flashLight.enabled = true;
            yield return new WaitForSeconds(0.05f);
            if (flash != null) flash.SetActive(false);
            if (flashLight != null) flashLight.enabled = false;
            hammer.localRotation = Quaternion.identity;
            if (lever == null)
            {
                yield return Advance();
                yield break;
            }
            // Ciclo da alavanca: abre (arma o cão e gira o tambor) e fecha com estalo.
            yield return new WaitForSeconds(0.08f);
            var rest = lever.localRotation;
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.12f)
            {
                lever.localRotation = rest * Quaternion.Euler(0f, 0f, leverAngle * t);
                hammer.localRotation = Quaternion.Euler(0f, 0f, hammerCock * t);
                yield return null;
            }
            lever.localRotation = rest * Quaternion.Euler(0f, 0f, leverAngle);
            yield return Advance();
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.1f)
            {
                lever.localRotation = rest * Quaternion.Euler(0f, 0f, leverAngle * (1f - t));
                yield return null;
            }
            lever.localRotation = rest;
            hammer.localRotation = Quaternion.identity;
            Sound(clickSound, 0.5f);
        }

        private void OnDry()
        {
            Play(DryRoutine());
        }

        private IEnumerator DryRoutine()
        {
            yield return Tween(0.1f, k => hammer.localRotation = Quaternion.Euler(0f, 0f, hammerCock * k));
            Sound(clickSound);
            hammer.localRotation = Quaternion.identity;
            yield return Advance();
        }

        private IEnumerator Advance()
        {
            var from = -_turns * 60f;
            _turns++;
            _top = (_top + 1) % 6;
            var to = -_turns * 60f;
            yield return Tween(0.12f, k => cylinder.localRotation = Quaternion.Euler(Mathf.Lerp(from, to, k), 0f, 0f));
        }

        private void SetCrane(float k) => crane.localRotation = Quaternion.Euler(openAngle * k, 0f, 0f);

        // ---------- Recarga ----------
        private void OnReload(float time) => Play(ReloadRoutine(time));

        private void OnCancel()
        {
            if (_busy != null) StopCoroutine(_busy);
            _busy = null;
            SetCrane(0f);
            Refresh();
        }

        private IEnumerator ReloadRoutine(float total)
        {
            // tempos proporcionais à duração da recarga
            var open = total * 0.12f; var eject = total * 0.16f; var each = total * 0.09f; var close = total * 0.06f;
            Sound(openSound);
            yield return Tween(open, k => SetCrane(Mathf.Sin(k * Mathf.PI * 0.5f) * 1.06f - 0.06f * k));

            // ejetor: tudo sai para trás e cai
            for (var t = 0f; t < eject * 0.5f; t += Time.deltaTime)
            {
                for (var i = 0; i < 6; i++)
                    if (rounds[i] != null && _state[i] != Chamber.Empty)
                        rounds[i].transform.localPosition = _roundHome[i] + Vector3.left * (0.11f * t / (eject * 0.5f));
                yield return null;
            }
            for (var i = 0; i < 6; i++)
            {
                if (rounds[i] == null || _state[i] == Chamber.Empty) continue;
                var copy = Instantiate(rounds[i], rounds[i].transform.position, rounds[i].transform.rotation);
                copy.transform.localScale = rounds[i].transform.lossyScale;
                copy.layer = rounds[i].layer;
                foreach (Transform c in copy.transform) c.gameObject.layer = rounds[i].layer;
                copy.AddComponent<FallingCasing>();
                _state[i] = Chamber.Empty;
            }
            Refresh();
            yield return new WaitForSeconds(eject * 0.5f);

            // seis balas novas, uma por vez, a partir da câmara de cima
            for (var n = 0; n < 6; n++)
            {
                var i = (_top + n) % 6;
                _state[i] = Chamber.Live;
                Refresh();
                var r = rounds[i].transform;
                var home = _roundHome[i];
                yield return Tween(each * 0.8f, k => r.localPosition = home + Vector3.left * (0.18f * (1f - k)));
                Sound(roundSound, 0.5f);
                yield return new WaitForSeconds(each * 0.2f);
            }

            // fecha com um movimento seco
            yield return Tween(close, k => SetCrane(1f - k * 1.05f));
            Sound(closeSound);
            SetCrane(0f);
        }

        // ---------- Inspeção ----------
        private void OnInspect()
        {
            if (_busy == null) Play(InspectRoutine());
        }

        private IEnumerator InspectRoutine()
        {
            _modelRest = model.localRotation;
            // mostra a lateral do tambor: deita a arma e gira em volta do cano
            var tilt = _modelRest * Quaternion.Euler(0f, 65f, 0f) * Quaternion.Euler(-25f, 0f, 0f);
            yield return Tween(0.45f, k => model.localRotation = Quaternion.Slerp(_modelRest, tilt, k));
            Sound(openSound);
            yield return Tween(0.3f, k => SetCrane(k));
            var a0 = -_turns * 60f;
            yield return Tween(1.2f, k => cylinder.localRotation = Quaternion.Euler(a0 - 720f * (1f - (1f - k) * (1f - k)), 0f, 0f));
            cylinder.localRotation = Quaternion.Euler(a0, 0f, 0f);
            yield return new WaitForSeconds(0.25f);
            yield return Tween(0.1f, k => SetCrane(1f - k));
            Sound(closeSound);
            yield return Tween(0.4f, k => model.localRotation = Quaternion.Slerp(tilt, _modelRest, k));
        }
    }
}
