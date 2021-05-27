# BezierCurveEditor

This repo is a fork of [BezierCurveEditor by Arkham Interactive from Unity Asset Store](https://assetstore.unity.com/packages/tools/bezier-curve-editor-11278).

It differs from the version on the Asset Store - the first commit is the code by the original author, the rest are customizations made in the last couple of years we've been using the code. Those are mostly additions to existing functionality.

### no-point-gameobjects branch

The branch removes GameObjects for each bezier point and instead stores points as managed objects in the BezierCurve, thus removing the overhead of GameObjects and Components. Beware, it is WIP and there is currently no upgrade method from old GameObject-based BezierPoints to non-GameObject, upgrading will remove all current points.

### BezierArcApproximation

This script is a new addition, it wasn't present in the version from the Asset Store. It can be used to approximate the Bezier curve using circular arcs (to the given error threshold). It was ported from the [javascript library by Pomax](http://pomax.github.io/bezierinfo).

### Package manager

File hierarchy has been reorganized for importing via Unity Package Manager.


# Optimization

Add `BEZIER_POINT_NO_UPDATE` to Scripting Define Symbols to disable the Update method in BezierPoint class, which can boost performance in play mode when there are many bezier points in the scene. Note that if you move bezier points in play mode, you'll have to call `SetDirty()` on the curve manually when this optimization is enabled.


# Breaking changes

If you've used the Asset Store version and plan to switch to this one in existing project, you'll have to account for the following changes.

### Curve resolution behavior

Summary:

* Curve interpolation is now more homogenous across segments on same curve
* Curves in existing projects will change in appearance, and might impact performance until you update the values
* An automatic resolution recalculation will be done when Unity encounters old curves (on scene open, prefab instantiation, play, etc.)
* You might have to tweak the values on existing curves to desired precision
* You might have to update your code to revise how you calculate the resolution value

This change makes sure that all segments have approximately the same resolution - with resolution now meaning the same number of interpolated points **per unit of distance** across the whole curve.

Previously, a curve whose segments are very different in length (e.g. a curve made of 3 points, where one segment is short and one very long) would have all segments interpolated with the same number of points, causing short segments to be interpolated too densely, and long segments to be insufficiently precise, with visible poly-line shape.

Automatic curve resolution recalculation will be done when Unity encounters old curves (on scene open, prefab instantiation, play, etc.), which will update the resolution value by dividing the old resolution by the length of the shortest curve segment. This will cause the longer segments have more interpolated points than before, but will prevent loss of precision on the shortest segment.

### GameObjectless Curve Points

The original package had GameObjects for each curve point as "BezierPoint" components. The no-point-gameobjects branch removes both components and GameObjects and uses an array of serialized pure C# "CurvePoint" classes instead. Simplifying code and optimizing the scene as a result.

The points now have just a position and handles and no longer have full transforms, so they don't have separate rotation and scale, as they just add unnecessary complexity.

#### How to upgrade

There is an automatic upgrade system that takes the old BezierPoints and converts them to CurvePoints, but needs to be used carefully:
* Before moving to a no-point-gameobjects branch, open an empty scene with no BezierCurves visible
* Checkout the no-point-gameobjects branch and let it recompile
* Now open each of the scenes that contains BezierCurves. The upgrade will run automatically and output a message if the upgrade went successfully. Save each of the scenes.
* For BezierCurves that are part of prefabs, you will need to manually remove old children GameObjects in those prefabs and reapply the prefabs.

#### API Changes

Most of the functions are still exactly the same, here are the differences:
* The point class name has changed from BezierPoint to CurvePoint (BezierPoint still exists for upgrade purposes);
* BezierPoints do not have Transforms any more, so in case you were using them, you will have to bypass those. The points only store position relative to the BezierCurve now (and handle positions). Rotation and scale no longer exists.