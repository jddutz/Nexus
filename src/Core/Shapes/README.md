# World-space shape queries

`AxisAlignedBoundingBox`, `OrientedBoundingBox`, `Circle`, `Sphere`, and `Capsule`
are a finite set of geometric components. They derive geometry from their owners;
`Nexus.Core.Geometry.Intersection` and `Distance` perform the calculations. There
is no physics integration, response, or contact solver.

`IShape.IntersectsWith(IShape)` delegates to a single symmetric dispatcher. Unknown
primitive pairs throw `NotSupportedException`. Supporting another primitive means
extending the shared algorithms and dispatch table. Components remain unsealed
because the observable-property generator requires virtual setters.

For queries outside the component system, pass Silk math values or the immutable
`CircleGeometry`, `OrientedBox`, and `CapsuleGeometry` snapshots to `Intersection`.
`Distance` supplies closest points and squared distances for points, segments,
axis-aligned boxes, triangles, and segment-to-oriented-box queries. Capsule
intersections compose those operations rather than approximating the capsule
with overlapping spheres. Oriented-box queries retain the transformed box,
including affine shear, rather than substituting its axis-aligned envelope.

## Query conventions

- Boundary contact counts as intersection.
- Ray directions need not be normalized. Hit distance is in world units. An
  interior origin hits at zero; a miss returns zero distance. Zero-length rays miss.
- `Box2D`, rectangles, and circle intersections evaluate the XY projection. This
  includes component pairs containing a `Circle`. A circle ray query instead
  intersects its world XY disk at the transformed center's Z coordinate.
- Other component pairs use all three world coordinates. Capsule has one
  representation: segment endpoints and radius. The query determines whether
  its XY projection or its 3D volume is evaluated.
- AABBs translate their offsets by the owner's world origin; rotation and scale
  do not affect their offsets. OBBs transform their complete local box.
- Circles, spheres, and capsules use the absolute radius. Nonuniform scale and
  shear produce conservative enclosing circular radii, rather than ellipses or
  ellipsoids. Circle radii use maximum projected XY scale; sphere and capsule
  radii use maximum 3D scale. Capsule endpoints use the complete world transform.
- Inputs are finite, box minima do not exceed maxima, and owner transforms are
  affine. Zero-length segments and zero box extents are valid geometry.
