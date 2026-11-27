BEGIN;

CREATE TEMP TABLE demo_landings (
    id uuid PRIMARY KEY,
    slug text UNIQUE NOT NULL,
    owner text NOT NULL,
    title text NOT NULL,
    published boolean NOT NULL
) ON COMMIT DROP;

INSERT INTO demo_landings VALUES
    ('f5e40c57-d0ad-495a-b534-4aa374ec59a1', 'seed-course', 'partner-a', 'Летний курс', true),
    ('f5e40c57-d0ad-495a-b534-4aa374ec59a2', 'seed-draft', 'partner-a', 'Черновик курса', false),
    ('f5e40c57-d0ad-495a-b534-4aa374ec59a3', 'seed-partner-b', 'partner-b', 'Курс партнёра B', true);

-- Refuse to replace a page whose ID belongs to a different record.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM demo_landings d JOIN landings l ON l."Id" = d.id
        WHERE l."Slug" <> d.slug OR l."PartnerId" <> d.owner
    ) THEN
        RAISE EXCEPTION 'A seed ID belongs to a different landing';
    END IF;
END;
$$;

INSERT INTO landings ("Id", "Slug", "PartnerId", "Draft", "Revision", "PublishedRevision")
SELECT id, slug, owner,
    jsonb_build_object(
        'name', title, 'type', 'course', 'schemaVersion', 1,
        'theme', jsonb_build_object('accent', '#7C3AED'),
        'blocks', jsonb_build_array(
            jsonb_build_object('id', 'intro', 'type', 'heading',
                'props', jsonb_build_object('title', title)),
            jsonb_build_object('id', 'offer', 'type', 'promo',
                'segments', jsonb_build_array('member'),
                'props', jsonb_build_object('title', 'Предложение для участников'))
        )
    ),
    1, NULL
FROM demo_landings
ON CONFLICT ("Id") DO UPDATE
SET "Draft" = EXCLUDED."Draft", "Revision" = landings."Revision" + 1,
    "PublishedRevision" = NULL;

INSERT INTO landing_publications ("LandingId", "Revision", "Content", "PublishedAt")
SELECT l."Id", l."Revision", l."Draft", CURRENT_TIMESTAMP
FROM landings l JOIN demo_landings d ON l."Id" = d.id AND d.published;

UPDATE landings l
SET "PublishedRevision" = l."Revision"
FROM demo_landings d WHERE l."Id" = d.id AND d.published;

SELECT l."Slug", l."Id", l."PartnerId", l."Revision", l."PublishedRevision"
FROM landings l JOIN demo_landings d ON l."Id" = d.id ORDER BY l."Slug";

COMMIT;
