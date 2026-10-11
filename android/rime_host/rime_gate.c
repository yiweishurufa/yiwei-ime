/* 一维输入法 CI gate: prove the packaged Rime data types correctly with the librime Trime bundles.
 *
 * usage: rime_gate <shared_dir> <user_dir> [--no-table-rebuild] <case>...
 *        rime_gate --version
 * case:  schema:keys:expected_candidate[:expected_preedit]
 *        e.g. rime_ice:nihao:你好   t9:64426:你好:ni hao
 *
 * Like the phone on first start: shared_dir is the APK's assets/shared (with the precompiled build/),
 * user_dir holds only what Trime itself writes there (default.custom.yaml). Every case is typed twice:
 *   1. right away, before any deployment: only the precompiled data is there (what the user gets the
 *      moment the keyboard opens; a dictionary this librime cannot read gives no candidates here);
 *   2. after the start-up maintenance Trime runs (which may refresh small files in user_dir/build).
 * Fails (exit 1) unless the expected candidate is on the first page (and the preedit matches, when given).
 * --no-table-rebuild also fails if the maintenance had to rebuild a dictionary table (*.table.bin /
 * *.reverse.bin in user_dir/build): the precompiled tables do not match and the phone would spend
 * minutes rebuilding the 60 MB rime_ice table.
 */
#include <dirent.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include <rime_api.h>

static int fail = 0;

static int staging_bins(const char* user_dir, int tables_only) {
  char path[4096];
  snprintf(path, sizeof path, "%s/build", user_dir);
  DIR* d = opendir(path);
  if (!d) return 0;
  int n = 0;
  struct dirent* e;
  while ((e = readdir(d))) {
    size_t len = strlen(e->d_name);
    if (len > 4 && strcmp(e->d_name + len - 4, ".bin") == 0) {
      int table = strstr(e->d_name, ".table.bin") || strstr(e->d_name, ".reverse.bin");
      printf("  compiled at start-up: build/%s%s\n", e->d_name, table ? "  <- dictionary table" : "");
      if (table || !tables_only) n++;
    }
  }
  closedir(d);
  return n;
}

static void run_case(RimeApi* rime, RimeSessionId s, char* spec) {
  char* schema = strtok(spec, ":");
  char* keys = strtok(NULL, ":");
  char* expect = strtok(NULL, ":");
  char* preedit_expect = strtok(NULL, "");
  if (!schema || !keys || !expect) {
    printf("FAIL bad case\n");
    fail = 1;
    return;
  }
  rime->clear_composition(s);
  if (!rime->select_schema(s, schema)) {
    printf("FAIL %s: cannot select schema\n", schema);
    fail = 1;
    return;
  }
  RIME_STRUCT(RimeStatus, status);
  if (rime->get_status(s, &status)) {
    printf("schema %s (%s)\n", status.schema_id ? status.schema_id : "?", status.schema_name ? status.schema_name : "?");
    rime->free_status(&status);
  }
  rime->simulate_key_sequence(s, keys);
  RIME_STRUCT(RimeContext, ctx);
  if (!rime->get_context(s, &ctx)) {
    printf("FAIL %s %s: no context\n", schema, keys);
    fail = 1;
    return;
  }
  const char* preedit = ctx.composition.preedit ? ctx.composition.preedit : "";
  int found = 0;
  printf("  %s %s -> preedit [%s] candidates:", schema, keys, preedit);
  for (int i = 0; i < ctx.menu.num_candidates; i++) {
    const char* t = ctx.menu.candidates[i].text;
    printf(" %s", t);
    if (strcmp(t, expect) == 0) found = 1;
  }
  printf("\n");
  if (!found) {
    printf("FAIL %s %s: %s not on the first page\n", schema, keys, expect);
    fail = 1;
  }
  if (preedit_expect && strcmp(preedit, preedit_expect) != 0) {
    printf("FAIL %s %s: preedit [%s], expected [%s]\n", schema, keys, preedit, preedit_expect);
    fail = 1;
  }
  rime->free_context(&ctx);
  rime->clear_composition(s);
}

static void run_cases(RimeApi* rime, int argc, char* argv[], int first, const char* phase) {
  printf("== %s\n", phase);
  RimeSessionId s = rime->create_session();
  if (!s) {
    printf("FAIL cannot create session\n");
    fail = 1;
    return;
  }
  for (int i = first; i < argc; i++) {
    char spec[1024];
    snprintf(spec, sizeof spec, "%s", argv[i]);
    run_case(rime, s, spec);
  }
  rime->destroy_session(s);
}

int main(int argc, char* argv[]) {
  RimeApi* rime = rime_get_api();
  if (argc == 2 && strcmp(argv[1], "--version") == 0) {
    printf("librime %s\n", rime->get_version());
    return 0;
  }
  if (argc < 4) {
    fprintf(stderr, "usage: %s <shared_dir> <user_dir> [--no-table-rebuild] <schema:keys:expected[:preedit]>...\n", argv[0]);
    return 2;
  }
  int first = 3, check_tables = 0;
  if (strcmp(argv[first], "--no-table-rebuild") == 0) {
    check_tables = 1;
    first++;
  }
  RIME_STRUCT(RimeTraits, traits);
  traits.shared_data_dir = argv[1];
  traits.user_data_dir = argv[2];
  traits.app_name = "rime.yiwei.gate";
  traits.distribution_name = "Yiwei";
  traits.distribution_code_name = "yiwei-gate";
  traits.distribution_version = "0";
  traits.min_log_level = 2; /* errors only */
  traits.log_dir = argv[2];
  printf("librime %s, shared %s, user %s\n", rime->get_version(), argv[1], argv[2]);
  rime->setup(&traits);
  rime->initialize(&traits);
  run_cases(rime, argc, argv, first, "first start, before deployment (precompiled data only)");
  if (rime->start_maintenance(False)) rime->join_maintenance_thread();
  int n = staging_bins(argv[2], 1);
  if (check_tables && n) {
    printf("FAIL %d dictionary table%s rebuilt at start-up: precompiled tables do not match this librime\n", n, n == 1 ? "" : "s");
    fail = 1;
  }
  run_cases(rime, argc, argv, first, "after start-up deployment");
  rime->finalize();
  printf(fail ? "RIME GATE FAILED\n" : "RIME GATE PASSED\n");
  return fail;
}
