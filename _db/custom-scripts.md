# Change odds for CorrectScore
```sql
START TRANSACTION;

-- 1) Update active/default payout configuration rules
UPDATE payout_configuration_market_rules r
JOIN payout_configurations c
  ON c.id = r.payout_configuration_id
SET r.payout_multiplier = 8.00
WHERE c.is_active = 1
  AND r.market_type = 'CorrectScore';

-- 2) Update all existing pool CorrectScore markets
UPDATE markets
SET payout_multiplier = 8.00
WHERE type = 'CorrectScore';

COMMIT;
```
# Change pool profile
```sql
START TRANSACTION;

SET @pool_id = 'YOUR_POOL_ID_HERE';

-- Safety check: should return 0 before you continue.
SELECT COUNT(*) AS prediction_count
FROM predictions
WHERE pool_id = @pool_id;

-- Stop if predictions exist.
-- If this returns a row, ROLLBACK instead of continuing.
SELECT 'STOP: pool has predictions; do not regenerate markets by SQL' AS warning
WHERE EXISTS (
  SELECT 1
  FROM predictions
  WHERE pool_id = @pool_id
);

-- Change pool profile.
UPDATE pools
SET profile = 'Casual'
WHERE id = @pool_id;

-- Remove current generated markets for this pool.
DELETE FROM markets
WHERE pool_id = @pool_id;

-- Regenerate Casual markets for every event in the pool tournament
-- using the active payout configuration.
INSERT INTO markets (
  id,
  pool_id,
  event_id,
  type,
  period,
  line_value,
  payout_multiplier,
  payout_configuration_version,
  status
)
SELECT
  UUID(),
  p.id,
  e.id,
  r.market_type,
  r.period,
  r.line_value,
  r.payout_multiplier,
  pc.version,
  CASE
    WHEN r.market_type = 'Handicap' THEN 'LinePending'
    ELSE 'Open'
  END
FROM pools p
JOIN events e
  ON e.tournament_id = p.tournament_id
JOIN payout_configurations pc
  ON pc.is_active = 1
JOIN payout_configuration_market_rules r
  ON r.payout_configuration_id = pc.id
WHERE p.id = @pool_id
  AND r.profile = 'Casual'
  AND r.is_enabled = 1;

COMMIT;
```
