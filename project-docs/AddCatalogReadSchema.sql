-- Additive compatibility update for existing development databases.
-- Mirrors Museum, Topic, ArtifactTopic, ArtifactMedia and Artifact3DModel.
-- Existing rows are retained; no demo museum, artifact or media is inserted.
BEGIN;
SET LOCAL lock_timeout = '5s';

ALTER TABLE public.museums
    ADD COLUMN IF NOT EXISTS "IndoorLayout" text NOT NULL DEFAULT 'unknown';

CREATE TABLE IF NOT EXISTS public.topics (
    "Id" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "MuseumId" integer NOT NULL REFERENCES public.museums("Id") ON DELETE CASCADE,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(2000),
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "CreatedAt" timestamp with time zone NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_topics_MuseumId_Name" ON public.topics ("MuseumId", "Name");

CREATE TABLE IF NOT EXISTS public.artifact_topics (
    "ArtifactId" integer NOT NULL REFERENCES public.artifacts("Id") ON DELETE CASCADE,
    "TopicId" integer NOT NULL REFERENCES public.topics("Id") ON DELETE CASCADE,
    "AssignedAt" timestamp with time zone NOT NULL,
    PRIMARY KEY ("ArtifactId", "TopicId")
);
CREATE INDEX IF NOT EXISTS "IX_artifact_topics_TopicId" ON public.artifact_topics ("TopicId");

CREATE TABLE IF NOT EXISTS public.artifact_medias (
    "Id" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "ArtifactId" integer NOT NULL REFERENCES public.artifacts("Id") ON DELETE CASCADE,
    "MediaType" character varying(30) NOT NULL,
    "Title" character varying(300) NOT NULL,
    "LanguageCode" character varying(35),
    "FileName" character varying(300) NOT NULL,
    "StorageKey" character varying(200) NOT NULL,
    "ContentType" character varying(100) NOT NULL,
    "FileSize" bigint NOT NULL,
    "SortOrder" integer NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_artifact_medias_ArtifactId" ON public.artifact_medias ("ArtifactId");

CREATE TABLE IF NOT EXISTS public.artifact_3d_models (
    "Id" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "ArtifactId" integer NOT NULL REFERENCES public.artifacts("Id") ON DELETE CASCADE,
    "Name" character varying(300) NOT NULL,
    "FileName" character varying(300) NOT NULL,
    "StorageKey" character varying(200) NOT NULL,
    "FileFormat" character varying(20) NOT NULL,
    "ContentType" character varying(100) NOT NULL,
    "FileSize" bigint NOT NULL,
    "PosterUrl" character varying(1000),
    "AutoRotate" boolean NOT NULL,
    "CameraOrbit" character varying(100),
    "FieldOfView" character varying(50),
    "Scale" numeric(10,4) NOT NULL DEFAULT 1.0,
    "UpdatedBy" uuid NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_artifact_3d_models_ArtifactId" ON public.artifact_3d_models ("ArtifactId");
COMMIT;
