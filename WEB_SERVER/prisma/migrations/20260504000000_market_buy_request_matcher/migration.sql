 
-- Match buy requests with listed items and execute trade atomically
CREATE OR REPLACE FUNCTION process_buy_request_orders()
RETURNS INTEGER
LANGUAGE plpgsql
AS $$
DECLARE
  v_order RECORD;
  v_seller_item RECORD;
  v_buyer_next_seq INT;
  v_seller_balance_seq INT;
  v_buyer_balance_seq INT;
  v_trade_seq INT;
  v_processed_count INT := 0;
BEGIN
  FOR v_order IN
    SELECT bro.*
    FROM "BuyRequestOrder" bro
    WHERE bro.status = 0
    ORDER BY bro."createDate" ASC
  LOOP
    -- Lock one suitable selling item for this order.
    SELECT pi.*
    INTO v_seller_item
    FROM "PlayerItem" pi
    WHERE pi."itemId" = v_order."itemId"
      AND pi."Price" = v_order.price
      AND pi."IsSolded" = 1
      AND pi."playerId" <> v_order."playerId"
    ORDER BY pi.seq ASC
    LIMIT 1
    FOR UPDATE SKIP LOCKED;

    -- No suitable seller item found.
    IF NOT FOUND THEN
      CONTINUE;
    END IF;

    -- Mark seller item as sold.
    UPDATE "PlayerItem"
    SET "IsSolded" = 2
    WHERE "playerId" = v_seller_item."playerId"
      AND "itemId" = v_seller_item."itemId"
      AND seq = v_seller_item.seq;

    -- Prepare next buyer seq for the same item.
    SELECT COALESCE(MAX(seq), 0) + 1
    INTO v_buyer_next_seq
    FROM "PlayerItem"
    WHERE "playerId" = v_order."playerId"
      AND "itemId" = v_order."itemId";

    -- Insert bought item row for buyer.
    INSERT INTO "PlayerItem" (
      "playerId", "itemId", seq, level, description, "Price", damage, "IsSolded", "Isbought"
    )
    VALUES (
      v_order."playerId",
      v_seller_item."itemId",
      v_buyer_next_seq,
      v_seller_item.level,
      COALESCE(v_seller_item.description, ''),
      0,
      v_seller_item.damage,
      0,
      1
    );

    -- Balance history seq for seller.
    SELECT COALESCE(MAX(seq), 0) + 1
    INTO v_seller_balance_seq
    FROM "BalanceHistory"
    WHERE "userId" = v_seller_item."playerId";

    -- Balance history seq for buyer.
    SELECT COALESCE(MAX(seq), 0) + 1
    INTO v_buyer_balance_seq
    FROM "BalanceHistory"
    WHERE "userId" = v_order."playerId";

    -- Credit seller.
    INSERT INTO "BalanceHistory" ("userId", seq, "ringBall", money, description, "eventType")
    VALUES (
      v_seller_item."playerId",
      v_seller_balance_seq,
      0,
      v_order.price,
      'Sell item #' || v_order."itemId" || ' to player #' || v_order."playerId",
      'MARKET_SELL'
    );

    -- Debit buyer.
    INSERT INTO "BalanceHistory" ("userId", seq, "ringBall", money, description, "eventType")
    VALUES (
      v_order."playerId",
      v_buyer_balance_seq,
      0,
      -v_order.price,
      'Buy item #' || v_order."itemId" || ' from player #' || v_seller_item."playerId",
      'MARKET_BUY'
    );

    -- Item trade history seq.
    SELECT COALESCE(MAX(seq), 0) + 1
    INTO v_trade_seq
    FROM "ItemTradeHistory"
    WHERE "playerIdBuy" = v_order."playerId"
      AND "playerIdSold" = v_seller_item."playerId"
      AND "itemId" = v_order."itemId";

    INSERT INTO "ItemTradeHistory" (
      "playerIdBuy", "playerIdSold", "itemId", seq
    )
    VALUES (
      v_order."playerId",
      v_seller_item."playerId",
      v_order."itemId",
      v_trade_seq
    );

    -- Mark order as processed.
    UPDATE "BuyRequestOrder"
    SET status = 1
    WHERE "playerId" = v_order."playerId"
      AND "itemId" = v_order."itemId"
      AND seq = v_order.seq
      AND status = 0;

    v_processed_count := v_processed_count + 1;
  END LOOP;

  RETURN v_processed_count;
END;
$$;

-- Optional: schedule every 5 seconds (requires pg_cron extension).
CREATE EXTENSION IF NOT EXISTS pg_cron;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1
    FROM cron.job
    WHERE jobname = 'process_buy_request_orders_every_5s'
  ) THEN
    PERFORM cron.schedule(
      'process_buy_request_orders_every_5s',
      '5 seconds',
      $$SELECT process_buy_request_orders();$$
    );
  END IF;
END
$$;
