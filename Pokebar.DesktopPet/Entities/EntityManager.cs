using System.Collections.Generic;
using System.Linq;

namespace Pokebar.DesktopPet.Entities;

public class EntityManager
{
    private readonly List<BaseEntity> _entities = new();

    public IReadOnlyList<BaseEntity> Entities => _entities;
    public IEnumerable<PlayerPet> Players => _entities.OfType<PlayerPet>();
    public IEnumerable<EnemyPet> Enemies => _entities.OfType<EnemyPet>();

    public void Add(BaseEntity entity)
    {
        if (!_entities.Contains(entity))
            _entities.Add(entity);
    }

    public bool Remove(BaseEntity entity)
    {
        return _entities.Remove(entity);
    }

    public void Update(double deltaTime)
    {
        foreach (var entity in _entities)
        {
            entity.Update(deltaTime);
        }
    }

    public void RemoveInactive()
    {
        _entities.RemoveAll(entity =>
            entity.State == EntityState.Captured ||
            entity.State == EntityState.Dead);
    }
}
