<template>
  <div v-if="err" class="alert alert-danger">{{ err }}</div>
  <div v-else>
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
      <div>
        <h5 class="mb-0">模块：{{ module?.moduleName || module?.name }}</h5>
        <div class="text-muted small">评鉴：{{ auditId }} · 模块：{{ moduleId }} · {{ lockInfo }}</div>
      </div>
      <div class="d-flex gap-2">
        <a class="btn btn-outline-secondary" :href="`#/audits/${auditId}`">返回</a>
        <button class="btn btn-outline-primary" :disabled="busySubmit" @click="submitModule">提交完成</button>
        <button class="btn btn-outline-secondary" :disabled="busyWithdraw" @click="withdrawModule">撤回</button>
      </div>
    </div>

    <div class="alert alert-warning" v-if="lockedByOther">
      当前模块被 <b>{{ lockOwner }}</b> 编辑中（同一模块同一时间只允许一人编辑）。你可以查看，但不能保存。
    </div>

    <div class="card card-soft shadow-sm">
      <div class="card-body">
        <div class="d-flex justify-content-between align-items-center">
          <h6 class="mb-0">条款（{{ clauses.length }}）</h6>
          <button class="btn btn-sm btn-outline-secondary" @click="load" :disabled="busy">刷新</button>
        </div>

        <div class="mt-3" v-for="c in clauses" :key="c.clauseCode" :id="`clause-${c.clauseCode}`">
          <div class="border rounded p-3">
            <div class="d-flex justify-content-between align-items-start gap-2">
              <div>
                <div class="fw-semibold">
                  <span class="badge bg-dark me-2">{{ c.level || c.grade || '-' }}</span>
                  {{ c.clauseText || c.content || c.title }}
                </div>
                <div class="text-muted small">条款：{{ c.clauseCode }}</div>
              </div>
              <div class="text-end">
                <select class="form-select form-select-sm" style="min-width:160px" v-model="c.status" :disabled="readOnly">
                  <option value="">未填写</option>
                  <option value="1">符合</option>
                  <option value="2">部分不符合</option>
                  <option value="3">不符合</option>
                  <option value="4">不适用</option>
                </select>
                <div class="small mt-1" :class="statusClass(c.status)">
                  {{ statusLabel(c.status) }}
                </div>
              </div>
            </div>

            <div class="row g-2 mt-2">
              <div class="col-12">
                <label class="form-label small">描述（必填）</label>
                <textarea class="form-control" rows="3" v-model="c.remark" :disabled="readOnly" placeholder="请输入现场记录/说明"></textarea>
              </div>

              <div class="col-12">
                <div class="d-flex justify-content-between align-items-center">
                  <div class="small text-muted">照片证据（最多 3 张，每张 ≤ 10MB，仅图片）</div>
                  <div class="d-flex gap-2">
                    <input class="form-control form-control-sm" type="file" accept="image/*" capture="environment" :disabled="readOnly || (c.photos?.length||0)>=3" @change="e => onPickPhoto(c, e, true)" />
                    <input class="form-control form-control-sm" type="file" accept="image/*" :disabled="readOnly || (c.photos?.length||0)>=3" @change="e => onPickPhoto(c, e, false)" />
                  </div>
                </div>

                <div class="d-flex flex-wrap gap-2 mt-2">
                  <div v-for="p in (c.photos||[])" :key="p.photoId || p.id || p.url" class="text-center">
                    <img :src="p.url" class="img-thumb" @click="preview(p.url)" />
                    <div class="small text-muted">#{{ p.sortNo ?? '-' }}</div>
                  </div>
                </div>

                <div class="d-flex flex-wrap gap-2 mt-2" v-if="c.lastYearPhotos && c.lastYearPhotos.length">
                  <div class="small text-muted w-100">去年参考（仅显示链接/缩略图，不复制到今年）</div>
                  <div v-for="p in c.lastYearPhotos" :key="p.url" class="text-center">
                    <img :src="p.url" class="img-thumb" @click="preview(p.url)" />
                    <div class="small text-muted">参考</div>
                  </div>
                </div>

              </div>
            </div>

            <div class="sticky-actions mt-3">
              <div class="d-flex justify-content-between align-items-center">
                <div class="text-danger small" v-if="c._err">{{ c._err }}</div>
                <div class="d-flex gap-2">
                  <button class="btn btn-sm btn-outline-secondary" :disabled="readOnly || c._saving" @click="resetClause(c)">重置</button>
                  <button class="btn btn-sm btn-primary" :disabled="readOnly || c._saving" @click="saveClause(c)">
                    {{ c._saving ? '保存中…' : '保存' }}
                  </button>
                </div>
              </div>
            </div>

          </div>
        </div>

      </div>
    </div>
  </div>

  <!-- preview modal -->
  <div class="modal fade" id="pv" tabindex="-1">
    <div class="modal-dialog modal-dialog-centered modal-lg">
      <div class="modal-content">
        <div class="modal-body">
          <img :src="previewUrl" style="width:100%" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { AuditsApi } from '../api/endpoints'
import { apiErrorMessage } from '../api/http'

const route = useRoute()
const auditId = route.params.auditId
const moduleId = route.params.moduleId

const err = ref('')
const busy = ref(false)
const module = ref(null)
const clauses = ref([])

const lockOwner = ref('')
const lockInfo = ref('')
const lockedByOther = ref(false)

const busySubmit = ref(false)
const busyWithdraw = ref(false)

let hbTimer = null

const readOnly = computed(() => lockedByOther.value)

function normalizeClause(raw) {
  const c = {
    clauseCode: raw.clauseCode || raw.code || raw.id,
    clauseText: raw.clauseText || raw.content || raw.title,
    level: raw.level || raw.grade,
    status: String(raw.status ?? raw.checkResult ?? ''),
    remark: raw.remark || raw.comment || '',
    photos: raw.photos || raw.curPhotos || [],
    lastYearPhotos: raw.lastYearPhotos || raw.refPhotos || []
  }
  c._origin = { status: c.status, remark: c.remark }
  c._saving = false
  c._err = ''
  return c
}

function statusLabel(v) {
  switch (String(v)) {
    case '1': return '符合'
    case '2': return '部分不符合'
    case '3': return '不符合'
    case '4': return '不适用'
    default: return '未填写'
  }
}

function statusClass(v) {
  const s = String(v)
  if (s === '3') return 'text-danger fw-semibold'
  if (s === '2') return 'text-warning fw-semibold'
  if (s === '4') return 'text-muted'
  if (s === '1') return 'text-success'
  return 'text-muted'
}

async function lock() {
  try {
    const data = await AuditsApi.lockModule(auditId, moduleId)
    // assume { ok, lockedBy, locked, owner }
    const owner = data?.owner || data?.lockedBy || data?.userName || ''
    const ok = data?.ok ?? true
    if (data?.locked === false || ok === false) {
      lockedByOther.value = true
      lockOwner.value = owner || '其他用户'
      lockInfo.value = '（只读）'
    } else {
      lockedByOther.value = false
      lockOwner.value = ''
      lockInfo.value = '（已锁定）'
    }
  } catch (e) {
    // if lock endpoint not implemented, allow editing
    lockInfo.value = ''
  }
}

async function heartbeat() {
  try { await AuditsApi.heartbeatModule(auditId, moduleId) } catch {}
}

async function unlock() {
  try { await AuditsApi.unlockModule(auditId, moduleId) } catch {}
}

async function load() {
  err.value = ''
  busy.value = true
  try {
    const data = await AuditsApi.moduleDetail(auditId, moduleId)
    module.value = data?.module || data?.Module || data
    const list = data?.clauses || data?.items || data?.Clauses || module.value?.clauses || []
    clauses.value = (Array.isArray(list) ? list : []).map(normalizeClause)
  } catch (e) {
    err.value = apiErrorMessage(e)
  } finally {
    busy.value = false
  }
}

async function saveClause(c) {
  c._err = ''
  if (!c.remark || !c.remark.trim()) {
    c._err = '描述必填'
    return
  }
  if (!c.status) {
    c._err = '请选择状态'
    return
  }
  c._saving = true
  try {
    await AuditsApi.saveClause(auditId, c.clauseCode, {
      status: Number(c.status),
      remark: c.remark
    })
    c._origin = { status: c.status, remark: c.remark }
  } catch (e) {
    c._err = apiErrorMessage(e)
  } finally {
    c._saving = false
  }
}

function resetClause(c) {
  c.status = c._origin.status
  c.remark = c._origin.remark
  c._err = ''
}

async function onPickPhoto(c, evt, isCamera) {
  const file = evt.target.files?.[0]
  evt.target.value = ''
  if (!file) return
  if (!file.type.startsWith('image/')) {
    c._err = '只允许上传照片'
    return
  }
  if (file.size > 10 * 1024 * 1024) {
    c._err = '单张照片不能超过 10MB'
    return
  }
  if ((c.photos?.length || 0) >= 3) {
    c._err = '最多 3 张'
    return
  }
  try {
    const sortNo = (c.photos?.length || 0) + 1
    const res = await AuditsApi.uploadClausePhoto(auditId, c.clauseCode, file, sortNo)
    // backend may return new list or single
    if (Array.isArray(res)) c.photos = res
    else if (res?.photos) c.photos = res.photos
    else c.photos = [...(c.photos || []), { url: res?.url || (`/api/files/${res?.photoId || ''}`), sortNo }]
  } catch (e) {
    c._err = apiErrorMessage(e)
  }
}

async function submitModule() {
  busySubmit.value = true
  try { await AuditsApi.submitModule(auditId, moduleId); await load() } finally { busySubmit.value = false }
}

async function withdrawModule() {
  busyWithdraw.value = true
  try { await AuditsApi.withdrawModule(auditId, moduleId); await load() } finally { busyWithdraw.value = false }
}

const previewUrl = ref('')
function preview(url) {
  previewUrl.value = url
  const el = document.getElementById('pv')
  if (!el) return
  // eslint-disable-next-line no-undef
  const modal = new bootstrap.Modal(el)
  modal.show()
}

onMounted(async () => {
  await lock()
  await load()
  hbTimer = setInterval(heartbeat, 20000)
})

onBeforeUnmount(async () => {
  if (hbTimer) clearInterval(hbTimer)
  await unlock()
})
</script>
