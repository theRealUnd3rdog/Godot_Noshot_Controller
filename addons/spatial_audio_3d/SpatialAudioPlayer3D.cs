using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

public partial class SpatialAudioPlayer3D : AudioStreamPlayer3D
{
    [Export] private float _maxRaycastDistance = 30.0f;
    [Export] private float _updateFrequencySeconds = 0.5f;
    [Export] private float _maxReverbWetness = 0.5f;
    [Export] private bool _useLowpassEffect = true;
    [Export] private int _wallLowpassCutoffAmt = 600;
    [Export] private float _lerpSpeedModifier = 1.0f;
    

    // Raycasts
    [Export] private RayCast3D _raycastDown;
    [Export] private RayCast3D _raycastLeft;
    [Export] private RayCast3D _raycastRight;
    [Export] private RayCast3D _raycastForward;
    [Export] private RayCast3D _raycastForwardLeft;
    [Export] private RayCast3D _raycastForwardRight;
    [Export] private RayCast3D _raycastBackwardRight;
    [Export] private RayCast3D _raycastBackwardLeft;
    [Export] private RayCast3D _raycastBackward;
    [Export] private RayCast3D _raycastUp;


    private List<RayCast3D> _raycastList = new List<RayCast3D>();
    private List<float> _distanceList = new List<float>(){0, 0, 0, 0, 0, 0, 0, 0, 0, 0};
    private float _lastUpdateTime = 0.0f;
    private bool _updateDistances = true;
    private int _currentRaycastIndex = 0;

    // Audio bus for this spatial audio player
    private int _audioBusIndex = 0;
    private string _audioBusName = "";

    // Effects
    private AudioEffectReverb _reverbEffect;
    private AudioEffectLowPassFilter _lowpassFilter;

    // Target paramaters
    private float _targetLowpassCutoff = 20000;
    private float _targetReverbRoomSize = 0.0f;
    private float _targetReverbWetness = 0.0f;
    private float _targetVolumeDb = 0.0f;

    public override void _Ready()
    {
        // Create audio bus to control the effects
        _audioBusIndex = AudioServer.BusCount;
        _audioBusName = "SpatialBus#" + _audioBusIndex.ToString();

        AudioServer.AddBus(_audioBusIndex);
        AudioServer.SetBusName(_audioBusIndex, _audioBusName);
        AudioServer.SetBusSend(_audioBusIndex, AudioServer.GetBusName(1));
        this.Bus = _audioBusName;

        // Add the effects to the custom audio bus
        AudioServer.AddBusEffect(_audioBusIndex, new AudioEffectReverb(), 0);
        _reverbEffect = AudioServer.GetBusEffect(_audioBusIndex, 0) as AudioEffectReverb;

        AudioServer.AddBusEffect(_audioBusIndex, new AudioEffectLowPassFilter(), 1);
        _lowpassFilter = AudioServer.GetBusEffect(_audioBusIndex, 1) as AudioEffectLowPassFilter;
        
        // Capture the target volume, we will start from no sound and lerp to where it should be
        _targetVolumeDb = VolumeDb;
        VolumeDb = -60.0f;

        // Initialize the raycast max distances
        _raycastDown.TargetPosition = new Vector3(0, -_maxRaycastDistance, 0);
        _raycastLeft.TargetPosition = new Vector3(_maxRaycastDistance, 0, 0);
        _raycastRight.TargetPosition = new Vector3(-_maxRaycastDistance, 0, 0);
        _raycastForward.TargetPosition = new Vector3(0, 0, _maxRaycastDistance);
        _raycastForwardLeft.TargetPosition = new Vector3(0, 0, _maxRaycastDistance);
        _raycastBackwardRight.TargetPosition = new Vector3(0, 0, -_maxRaycastDistance);
        _raycastBackwardLeft.TargetPosition = new Vector3(0, 0, -_maxRaycastDistance);
        _raycastBackward.TargetPosition = new Vector3(0, 0, -_maxRaycastDistance);
        _raycastUp.TargetPosition = new Vector3(0, _maxRaycastDistance, 0);

        // Append them to the list
        _raycastList.Add(_raycastDown);
        _raycastList.Add(_raycastLeft);
        _raycastList.Add(_raycastRight);
        _raycastList.Add(_raycastForward);
        _raycastList.Add(_raycastForwardLeft);
        _raycastList.Add(_raycastForwardRight);
        _raycastList.Add(_raycastBackwardRight);
        _raycastList.Add(_raycastBackwardLeft);
        _raycastList.Add(_raycastBackward);
        _raycastList.Add(_raycastUp);
    }

    public override void _PhysicsProcess(double delta)
    {
        _lastUpdateTime += (float)delta;

        // Should we update the raycast distance values
        if (_updateDistances)
        {
            OnUpdateRaycastDistance(_raycastList[_currentRaycastIndex], _currentRaycastIndex);
            _currentRaycastIndex += 1;

            if (_currentRaycastIndex >= _distanceList.Count)
            {
                _currentRaycastIndex = 0;
                _updateDistances = false;
            }
        }

        // Check if we should update the spatial sound values
        if (_lastUpdateTime > _updateFrequencySeconds)
        {
            var playerCam = GetViewport().GetCamera3D();

            if (playerCam != null)
                OnUpdateSpatialAudio(playerCam);

            _updateDistances = true;
            _lastUpdateTime = 0.0f;
        }

        LerpParameters((float)delta);
    }

    private void OnUpdateRaycastDistance(RayCast3D ray, int rayIndex)
    {
        ray.ForceRaycastUpdate();

        var collider = ray.GetCollider();

        if (collider != null)
            _distanceList[rayIndex] = this.GlobalPosition.DistanceTo(ray.GetCollisionPoint());
        else
            _distanceList[rayIndex] = -1;

        ray.Enabled = false;
    }

    private void OnUpdateSpatialAudio(Node3D player)
    {
        OnUpdateReverb(player);

        if (_useLowpassEffect)
            OnUpdateLowpassFilter(player);
    }

    private void OnUpdateReverb(Node3D player)
    {
        if (_reverbEffect != null)
        {
            // Find reverb params
            float roomSize = 0.0f;
            float wetness = 0.0f;

            foreach (float dist in _distanceList)
            {
                if (dist >= 0)
                {
                    // Find the room size based on the distance
                    roomSize += Mathf.Clamp((dist / _maxRaycastDistance)/ (float)_distanceList.Count, 0.0f, 1.0f);

                    // Adjust the wetness based on the distance
                    // Direct relationship: as distance increases, wetness increases
                    wetness += Mathf.Clamp((dist / _maxRaycastDistance) / (float)_distanceList.Count, 0.0f, _maxReverbWetness);

                    //GD.Print("Distance: " + dist);
                }
                else
                {
                    // Handle the case where the distance is invalid
                    wetness -= 1.0f / (float)_distanceList.Count;
                    wetness = Mathf.Max(wetness, 0.0f);
                }
            }

            _targetReverbWetness = wetness;
            _targetReverbRoomSize = roomSize;
        }
    }

    private void OnUpdateLowpassFilter(Node3D player)
    {
        if (_lowpassFilter != null)
        {
            // Send ray in direction of wall
            PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;

            Vector3 rayOrigin = this.GlobalPosition;
            Vector3 rayEnd = this.GlobalPosition + (player.GlobalPosition - this.GlobalPosition).Normalized() * _maxRaycastDistance;

            PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd, _raycastForward.CollisionMask);
            var result = spaceState.IntersectRay(query);

            var lowpassCutoff = 20000f; // init value to where nothing gets cutoff

            if (result.ContainsKey("collider"))
            {
                result.TryGetValue("position", out Variant position);

                var rayDistance = this.GlobalPosition.DistanceTo(position.AsVector3());
                var distanceToPlayer = this.GlobalPosition.DistanceTo(player.GlobalPosition);
                float wallToPlayerRatio = rayDistance / Mathf.Max(distanceToPlayer , 0.001f);

                if (rayDistance < distanceToPlayer)
                    lowpassCutoff = _wallLowpassCutoffAmt * wallToPlayerRatio;
            }

            _targetLowpassCutoff = lowpassCutoff;
        }
    }

    private void LerpParameters(float delta)
    {
        VolumeDb = Mathf.Lerp(VolumeDb, _targetVolumeDb, delta);
        _lowpassFilter.CutoffHz = Mathf.Lerp(_lowpassFilter.CutoffHz, _targetLowpassCutoff, delta * 5.0f * _lerpSpeedModifier);

        _reverbEffect.Wet = Mathf.Lerp(_reverbEffect.Wet, _targetReverbWetness, delta * 5.0f * _lerpSpeedModifier);
        _reverbEffect.RoomSize = Mathf.Lerp(_reverbEffect.RoomSize, _targetReverbRoomSize, delta * 5.0f * _lerpSpeedModifier);

        /* GD.Print("Wetness: " + _reverbEffect.Wet);
        GD.Print("Roomsize: " + _reverbEffect.RoomSize); */

        //PitchScale = (float)Mathf.Max(Mathf.Lerp(PitchScale, Engine.TimeScale, delta * 5.0f / Engine.TimeScale), 0.01f * 1.0f);
    }
}
