using UnityEngine;

/// <summary>
/// In manual mode, detects cutter contact with the current wood piece,
/// applies gravure marks, and plays optional particles while cutting.
/// </summary>
public class CNCManualWoodEngraver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CNCMachine _machine;
    [SerializeField] private WoodSpawner _woodSpawner;

    [Tooltip("Tip transform of the meche/cutter used for contact checks.")]
    [SerializeField] private Transform _cutterTip;

    [Header("Contact Settings")]
    [Tooltip("Sphere radius for cutter/wood contact checks.")]
    [SerializeField] [Range(0.001f, 0.05f)] private float _contactRadius = 0.012f;

    [Tooltip("Max distance from cutter tip to wood surface for engraving.")]
    [SerializeField] [Range(0.001f, 0.06f)] private float _maxEngraveDistance = 0.025f;

    [Tooltip("Depth parameter passed to WoodPiece gravure.")]
    [SerializeField] [Range(0.0001f, 0.02f)] private float _engraveDepth = 0.0025f;

    [Tooltip("Brush size parameter passed to WoodPiece gravure.")]
    [SerializeField] [Range(0.0005f, 0.03f)] private float _brushSize = 0.004f;

    [Header("Particles (optional)")]
    [Tooltip("If true, the script creates a runtime particle system when none is assigned.")]
    [SerializeField] private bool _autoCreateParticles = true;

    [Tooltip("Optional particle system for wood dust/chips while engraving.")]
    [SerializeField] private ParticleSystem _engraveParticles;

    [Tooltip("Emission rate while cutter touches wood.")]
    [SerializeField] [Range(1f, 120f)] private float _particleRate = 35f;

    [Tooltip("Distance between cutter tip and particle spawn point along contact normal.")]
    [SerializeField] [Range(0f, 0.01f)] private float _particleSurfaceOffset = 0.001f;

    private bool _isEngraving;
    private MecheRotator _mecheRotator;
    private CNC_MecheRotation _legacyMecheRotator;

    private void Awake()
    {
        if (_machine == null)
            _machine = GetComponent<CNCMachine>();

        if (_woodSpawner == null)
            _woodSpawner = GetComponent<WoodSpawner>();

        if (_cutterTip == null)
        {
            Transform machineRoot = _machine != null ? _machine.transform : transform;
            _cutterTip = machineRoot.Find("cncCutter/spindleHolder/spindleFinal/meche");
        }

        if (_engraveParticles == null && _autoCreateParticles)
            _engraveParticles = CreateRuntimeParticles();

        if (_cutterTip != null)
        {
            _mecheRotator = _cutterTip.GetComponent<MecheRotator>();
            _legacyMecheRotator = _cutterTip.GetComponent<CNC_MecheRotation>();
        }

        StopParticlesImmediate();
    }

    private void OnEnable()
    {
        if (_machine != null)
            _machine.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (_machine != null)
            _machine.OnStateChanged -= HandleStateChanged;

        _isEngraving = false;
        StopParticlesImmediate();
    }

    private void Update()
    {
        if (!CanEngraveNow())
        {
            if (_isEngraving)
            {
                _isEngraving = false;
                StopParticlesImmediate();
            }

            return;
        }

        ProcessEngravingContact();
    }

    private bool CanEngraveNow()
    {
        if (_machine == null || _woodSpawner == null || _cutterTip == null)
            return false;

        if (_machine.CurrentState != CNCMachine.CNCState.Cutting)
            return false;

        if (_machine.CurrentMode != CNCMachine.CNCMode.Manual)
            return false;

        if (!IsCutterRotating())
            return false;

        WoodPiece wood = _woodSpawner.CurrentWoodPiece;
        return wood != null && wood.IsInMachine;
    }

    private void ProcessEngravingContact()
    {
        WoodPiece wood = _woodSpawner.CurrentWoodPiece;
        if (wood == null)
        {
            _isEngraving = false;
            StopParticlesImmediate();
            return;
        }

        Bounds woodBounds = wood.GetBounds();
        Vector3 tip = _cutterTip.position;

        Vector3 closest = woodBounds.ClosestPoint(tip);
        float distance = Vector3.Distance(tip, closest);
        if (distance > _maxEngraveDistance)
        {
            _isEngraving = false;
            StopParticlesImmediate();
            return;
        }

        Vector3 normal = (tip - closest);
        if (normal.sqrMagnitude < 0.000001f)
            normal = Vector3.up;
        else
            normal.Normalize();

        Collider woodCollider = wood.GetComponentInChildren<Collider>();
        if (woodCollider != null)
        {
            Vector3 castOrigin = tip - normal * (_contactRadius * 2f);
            if (woodCollider.Raycast(new Ray(castOrigin, normal), out RaycastHit hit, _contactRadius * 4f + _maxEngraveDistance))
            {
                closest = hit.point;
                normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : normal;
                distance = Vector3.Distance(tip, hit.point);
            }
        }

        if (distance > _maxEngraveDistance)
        {
            _isEngraving = false;
            StopParticlesImmediate();
            return;
        }

        _isEngraving = true;
        wood.ApplyEngraveAtWorldPoint(closest, normal, _engraveDepth, _brushSize);
        UpdateParticles(closest, normal);
    }

    private void HandleStateChanged(CNCMachine.CNCState state)
    {
        if (state != CNCMachine.CNCState.Cutting)
        {
            _isEngraving = false;
            StopParticlesImmediate();
        }
    }

    private void UpdateParticles(Vector3 contactPoint, Vector3 normal)
    {
        if (_engraveParticles == null)
            return;

        Transform pt = _engraveParticles.transform;
        pt.position = contactPoint + normal * _particleSurfaceOffset;
        pt.rotation = Quaternion.LookRotation(normal, Vector3.up);

        var emission = _engraveParticles.emission;
        emission.rateOverTime = _isEngraving ? _particleRate : 0f;

        if (_isEngraving)
        {
            if (!_engraveParticles.isPlaying)
                _engraveParticles.Play();
        }
        else
        {
            _engraveParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void StopParticlesImmediate()
    {
        if (_engraveParticles == null)
            return;

        var emission = _engraveParticles.emission;
        emission.rateOverTime = 0f;

        if (_engraveParticles.isPlaying)
            _engraveParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private ParticleSystem CreateRuntimeParticles()
    {
        GameObject go = new GameObject("CNC_EngraveParticles");
        go.transform.SetParent(transform, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.010f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.62f, 0.44f, 0.24f, 0.95f),
            new Color(0.35f, 0.24f, 0.13f, 0.75f)
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.003f;
        shape.angle = 24f;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.2f;
        noise.frequency = 0.6f;

        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0.05f, 0.20f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.75f, 0.55f, 0.31f), 0f),
                new GradientColorKey(new Color(0.30f, 0.21f, 0.12f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.8f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0.2f)
            )
        );

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;

        return ps;
    }

    private bool IsCutterRotating()
    {
        bool hasModern = _mecheRotator != null && _mecheRotator.isActiveAndEnabled;
        bool hasLegacy = _legacyMecheRotator != null && _legacyMecheRotator.isActiveAndEnabled;
        return hasModern || hasLegacy;
    }
}
