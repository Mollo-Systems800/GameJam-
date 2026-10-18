using UnityEngine;

public sealed class TurretEnemy : MonoBehaviour
{
    public float range = 12;
    public float fireInterval = 2.2f;
    public float projectileSpeed = 7;
    public int damage = 1;
    public GameObject baseVisualPrefab;
    public GameObject barrelVisualPrefab;
    public Transform barrelRoot;
    public Transform housingRoot, headVisualRoot, muzzleRoot;
    MazeBetaGame game;
    Transform barrel;
    float cooldown;
    float recoil;
    Vector3 headRest;

    public void Initialize(MazeBetaGame owner)
    {
        game = owner;
        cooldown = 1.5f;
        var baseReplacement=baseVisualPrefab?baseVisualPrefab:game.turretBaseVisualPrefab;
        if(baseReplacement && housingRoot)
        {
            housingRoot.gameObject.SetActive(false);
            game.CreatePrimitiveVisual(PrimitiveType.Cube,transform,new Vector3(.72f,.84f,.62f),Vector3.up*.42f,false,baseReplacement);
        }
        if (GetComponentsInChildren<Renderer>().Length == 0)
            game.CreatePrimitiveVisual(PrimitiveType.Cube, transform, new Vector3(0.65f, 0.8f, 0.65f), Vector3.up * 0.4f, false,
                baseVisualPrefab ? baseVisualPrefab : game.turretBaseVisualPrefab);
        barrel = barrelRoot;
        if (!barrel)
        {
            barrel = new GameObject("Turret barrel").transform;
            barrel.SetParent(transform, false);
            barrel.localPosition = Vector3.up * 0.85f;
        }
        if (barrel.GetComponentsInChildren<Renderer>().Length == 0)
            game.CreatePrimitiveVisual(PrimitiveType.Cube, barrel, new Vector3(0.22f, 0.22f, 0.65f), Vector3.forward * 0.35f, false,
                barrelVisualPrefab ? barrelVisualPrefab : game.turretBarrelVisualPrefab);
        else if(headVisualRoot && (barrelVisualPrefab||game.turretBarrelVisualPrefab))
        {
            headVisualRoot.gameObject.SetActive(false);
            headVisualRoot=game.CreatePrimitiveVisual(PrimitiveType.Cube,barrel,new Vector3(.74f,.34f,.68f),Vector3.forward*.12f,false,
                barrelVisualPrefab?barrelVisualPrefab:game.turretBarrelVisualPrefab).transform;
        }
        if(headVisualRoot)headRest=headVisualRoot.localPosition;
        if (!GetComponent<Collider>())
        {
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.center = Vector3.up * 0.4f;
            collider.size = new Vector3(0.65f, 0.8f, 0.65f);
        }
    }

    void Update()
    {
        if (!game || !game.IsPlaying) return;
        cooldown -= Time.deltaTime;
        recoil=Mathf.MoveTowards(recoil,0,Time.deltaTime*.5f);
        if(headVisualRoot)headVisualRoot.localPosition=headRest-Vector3.forward*recoil;
        Vector3 target = game.Player.transform.position + Vector3.up * 0.9f;
        Vector3 delta = target - barrel.position;
        if (delta.sqrMagnitude > range * range || delta.sqrMagnitude < 0.001f) return;
        barrel.rotation = Quaternion.LookRotation(delta);
        Vector3 muzzle = muzzleRoot?muzzleRoot.position:barrel.position + barrel.forward * 0.75f;
        Vector3 shot = target - muzzle;
        // The first solid hit must be the player; walls and other enemies block fire.
        if (cooldown <= 0 && Physics.Raycast(barrel.position, delta.normalized, out RaycastHit hit,
            delta.magnitude + 0.1f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<Player>() == game.Player)
        {
            game.FireProjectile(muzzle, shot.normalized, projectileSpeed, damage, transform);
            MazeAudioDirector.Play(MazeSound.PrinterFire,muzzle,.7f);
            cooldown = Mathf.Max(0.25f, fireInterval);
            recoil=.07f;
        }
    }
}
