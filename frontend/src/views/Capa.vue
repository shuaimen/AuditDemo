<template>
  <div class="d-flex justify-content-between align-items-center mb-3">
    <div>
      <h5 class="mb-0">整改项（CAPA）</h5>
      <div class="text-muted small">评鉴：{{ auditId }}</div>
    </div>
    <a class="btn btn-outline-secondary" :href="`#/audits/${auditId}`">返回评鉴单</a>
  </div>

  <div v-if="err" class="alert alert-danger">{{ err }}</div>

  <div class="card card-soft shadow-sm" v-for="c in items" :key="c.capaId || c.id">
    <div class="card-body">
      <div class="d-flex justify-content-between gap-2">
        <div>
          <div class="fw-semibold">
            <span class="badge bg-danger me-2">{{ c.level || c.grade || 'NG' }}</span>
            {{ c.clauseText || c.clauseContent || c.title }}
          </div>
          <div class="text-muted small">条款：{{ c.clauseCode }} · 状态：{{ c.status || c.capaStatus }}</div>
        </div>
        <div class="text-end">
          <button class="btn btn-sm btn-outline-secondary" @click="toggle(c)">{{ c._open ? '收起' : '展开' }}</button>
        </div>
      </div>

      <div v-if="c._open" class="mt-3">
        <div class="row g-2">
          <div class="col-12 col-md-6">
            <label class="form-label">整改措施</label>
            <textarea class="form-control" rows="3" v-model="c.correctiveAction" :disabled="false"></textarea>
          </div>
          <div class="col-12 col-md-3">
            <label class="form-label">责任人</label>
            <input class="form-control" v-model="c.owner" />
          </div>
          <div class="col-12 col-md-3">
            <label class="form-label">截止日期</label>
            <input class="form-control" type="date" v-model="c.dueDate" />
          </div>
        </div>

        <div class="mt-2">
          <label class="form-label">证据（最多 5 张图片）</label>
          <div class="d-flex flex-wrap gap-2">
            <div v-for="(p,idx) in (c.evidences||[])" :key="idx" class="text-center">
              <img :src="p.url" class="img-thumb" @click="preview(p.url)" />
              <div class="small text-muted">#{{ p.sortNo ?? idx+1 }}</div>
            </div>
          </div>
          <div class="d-flex gap-2 mt-2">
            <input class="form-control" type="file" accept="image/*" capture="environment" :disabled="(c.evidences||[]).length>=5" @change="e => onUploadEvidence(c, e, true)" />
            <input class="form-control" type="file" accept="image/*" :disabled="(c.evidences||[]).length>=5" @change="e => onUploadEvidence(c, e, false)" />
          </div>
          <div class="small text-muted mt-1">单张≤10MB；只允许照片。</div>
        </div>

        <div class="sticky-actions mt-3">
          <div class="d-flex flex-wrap gap-2">
            <button class="btn btn-primary" :disabled="c._busy" @click="save(c)">保存</button>
            <button class="btn btn-outline-primary" :disabled="c._busy" @click="submitEvidence(c)">提交证据</button>
            <button class="btn btn-outline-danger" :disabled="c._busy" @click="close(c)">关闭整改（需复核结论）</button>
            <span class="text-success align-self-center" v-if="c._ok">已保存</span>
            <span class="text-danger align-self-center" v-if="c._err">{{ c._err }}</span>
          </div>
        </div>
      </div>

    </div>
  </div>

  <div class="modal fade" id="pv" tabindex="-1">
    <div class="modal-dialog modal-lg modal-dialog-centered">
      <div class="modal-content">
        <div class="modal-header"><h6 class="modal-title">预览</h6>
          <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
        </div>
        <div class="modal-body">
          <img :src="previewUrl" class="img-fluid" v-if="previewUrl" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { CapaApi } from '../api/endpoints'
import { apiErrorMessage } from '../api/http'

const route = useRoute()
const auditId = route.params.auditId

const items = ref([])
const err = ref('')
const previewUrl = ref('')

function toggle(c){ c._open = !c._open }

async function load() {
  err.value = ''
  try {
    const data = await CapaApi.listByAudit(auditId)
    items.value = Array.isArray(data) ? data : (data.items || [])
    items.value.forEach(x => { x._open = false; x._busy = false; x._err = ''; x._ok = false })
  } catch (e) {
    err.value = apiErrorMessage(e)
  }
}

async function save(c) {
  c._busy = true; c._err=''; c._ok=false
  try {
    await CapaApi.update(c.capaId || c.id, {
      correctiveAction: c.correctiveAction,
      owner: c.owner,
      dueDate: c.dueDate
    })
    c._ok = true
  } catch (e) {
    c._err = apiErrorMessage(e)
  } finally {
    c._busy = false
  }
}

async function onUploadEvidence(c, ev) {
  const f = ev.target.files?.[0]
  ev.target.value = ''
  if (!f) return
  if (f.size > 10*1024*1024) { c._err = '单张照片不能超过 10MB'; return }
  if (!f.type.startsWith('image/')) { c._err = '只允许照片'; return }
  if ((c.evidences || []).length >= 5) { c._err = '最多 5 张'; return }
  const sortNo = (c.evidences || []).length + 1
  try {
    const res = await CapaApi.uploadEvidence(c.capaId || c.id, f, sortNo)
    c.evidences = Array.isArray(res) ? res : (res?.evidences || [...(c.evidences||[]), { url: res?.url || '', sortNo }])
  } catch (e) {
    c._err = apiErrorMessage(e)
  }
}

async function submitEvidence(c) {
  c._busy=true; c._err=''; c._ok=false
  try { await CapaApi.submitEvidence(c.capaId || c.id); await load() } catch(e){ c._err=apiErrorMessage(e) } finally { c._busy=false }
}

async function close(c) {
  const reviewConclusion = prompt('请输入复核结论（必填）：')
  if (!reviewConclusion) return
  c._busy=true; c._err=''; c._ok=false
  try { await CapaApi.close(c.capaId || c.id, reviewConclusion); await load() } catch(e){ c._err=apiErrorMessage(e) } finally { c._busy=false }
}

function preview(url){
  previewUrl.value = url
  const el = document.getElementById('pv')
  // eslint-disable-next-line no-undef
  const modal = new bootstrap.Modal(el)
  modal.show()
}

onMounted(load)
</script>
