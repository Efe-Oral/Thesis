using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.XR.CoreUtils;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Gaze;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
using UnityEngine.XR.Interaction.Toolkit.Utilities;
using UnityEngine.XR.Interaction.Toolkit.Utilities.Pooling;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

#if BURST_PRESENT
using Unity.Burst;
#endif

[SelectionBase]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[AddComponentMenu("XR/XR Grab Interactable", 11)]
[HelpURL("https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@2.0/api/UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable.html")]
#if BURST_PRESENT
[BurstCompile]
#endif
public class MyXRGrabInteractable : XRGrabInteractable
{
    protected override void Awake()
    {
        base.Awake();

        // Override the default values from the base class
        useDynamicAttach = true;
        movementType = MovementType.VelocityTracking;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    protected override void OnSelectEntering(SelectEnterEventArgs args)
    {
        base.OnSelectEntering(args);
    }

    protected override void OnSelectExiting(SelectExitEventArgs args)
    {
        base.OnSelectExiting(args);
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
    }

    protected override void InitializeDynamicAttachPose(IXRSelectInteractor interactor, Transform dynamicAttachTransform)
    {
        base.InitializeDynamicAttachPose(interactor, dynamicAttachTransform);
    }

    protected override bool ShouldMatchAttachPosition(IXRSelectInteractor interactor)
    {
        return base.ShouldMatchAttachPosition(interactor);
    }

    protected override bool ShouldMatchAttachRotation(IXRSelectInteractor interactor)
    {
        return base.ShouldMatchAttachRotation(interactor);
    }

    protected override bool ShouldSnapToColliderVolume(IXRSelectInteractor interactor)
    {
        return base.ShouldSnapToColliderVolume(interactor);
    }

    protected override void Grab()
    {
        base.Grab();
    }

    protected override void Drop()
    {
        base.Drop();
    }

    protected override void Detach()
    {
        base.Detach();
    }

    protected override void SetupRigidbodyGrab(Rigidbody rigidbody)
    {
        base.SetupRigidbodyGrab(rigidbody);
    }

    protected override void SetupRigidbodyDrop(Rigidbody rigidbody)
    {
        base.SetupRigidbodyDrop(rigidbody);
    }

    protected override void AddDefaultSingleGrabTransformer()
    {
        base.AddDefaultSingleGrabTransformer();
    }

    protected override void AddDefaultMultipleGrabTransformer()
    {
        base.AddDefaultMultipleGrabTransformer();
    }
}
