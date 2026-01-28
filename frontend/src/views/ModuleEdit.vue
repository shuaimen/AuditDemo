<template>
  <div class="card">
    <div style="display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap">
      <div>
        <router-link :to="'/audits/'+auditId">← 返回</router-link>
        <div style="font-size:20px;font-weight:700;margin-top:6px">模块录入</div>
      </div>
      <div>
        <span class="badge">锁定：{{ lockInfo }}</span>
        <button class="secondary" style="margin-left:10px" @click="reload">刷新</button>
      </div>
    </div>

    <div v-if="err" class="bad" style="margin-top:10px">{{ err }}</div>
  </div>

  <div class="card" v-if="locked">
    <div v-for="c in clauses" :key="c.clauseId" style="border-top:1px solid #eee;padding-top:12px;margin-top:12px">
      <div style="display:flex;gap:10px;flex-wrap:wrap;align-items:flex-start">
        <div style="min-width:110px">
          <div><b>{{ c.clauseCode }}</b></div>
          <div class="badge">等级 {{ c.clauseLevel }}</div>
        </div>
        <div style="flex:1 1 420px">
          <div style="white-space:pre-wrap">{{ c.content }}</div>
          <div style="color:#666;font-size:13px;margin-top:6px" v-if="c.lastYearStatus!=null">
            去年参考：{{ statusText(c.lastYearStatus) }}；{{ c.lastYearComment || '' }}
            <div v-if="c.lastYearPhotos && c.lastYearPhotos.length" style="display:flex;gap:8px;flex-wrap:wrap;margin-top:6px">
              <a v-for="p in c.lastYearPhotos" :key="p.photoId" :href="p.url" target="_blank">
                <img :src="p.url" style="width:70px;height:70px;object-fit:cover;border-radius:8px;border:1px solid #ddd;opacity:0.7" />
              </a>
            </div>
          </div>
        </div>
      </div>

      <div class="row" style="margin-top:10px">
        <div class="col">
          <label>结果</label>
          <select v-model.number="c.status">
            <option :value="0">未填写</option>
            <option :value="1">符合</option>
            <option :value="2">部分不符合</option>
            <option :value="3">不符合</option>
            <option :value="4">不适用</option>
          </select>
        </div>
        <div class="col">
          <label>文字描述</label>
          <textarea v-model="c.comment" placeholder="描述问题/现况/证据"></textarea>
        </div>
        <div class="col">
          <label>照片证据（最多3张，单张≤10MB）</label>
          <input type="file" accept="image/*" capture="environment" multiple @change="(e)=>uploadPhotos(c,e)" />
          <div style="display:flex;gap:8px;flex-wrap:wrap;margin-top:8px">
            <a v-for="p in c.photos" :key="p.photoId" :href="p.url" target="_blank">
              <img :src="p.url" style="width:86px;height:86px;object-fit:cover;border-radius:8px;border:1px solid #ddd" />
            </a>
          </div>
        </div>
      </div>

      <div style="margin-top:10px;display:flex;gap:10px;align-items:center">
        <button @click="saveClause(c)">保存</button>
        <span v-if="c._saving" style="color:#666">保存中...</span>
        <span v-if="c._saved" style="color:#2e7d32">已保存</span>
        <span v-if="isBad(c)" class="bad">不合格（会生成整改项）</span>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, onBeforeUnmount, ref } from 'vue'
import { http } from '../api/http'
import { useRoute } from 'vue-router'

const route = useRoute()
const auditId = route.params.auditId
const moduleId = route.params.moduleId

const clauses = ref([])
const err = ref('')
const locked = ref(false)
const lockToken = ref('')
const lockInfo = ref('未锁定')
let heartbeatTimer = null

function statusText(s) {
  switch (s) {
    case 1: return '符合'
    case 2: return '部分不符合'
    case 3: return '不符合'
    case 4: return '不适用'
    case 0:
    default: return '未填写'
  }
}

function isBad(c) {
  return c.status === 2 || c.status === 3
}

async function tryLock() {
  const r = await http.post(`/api/audits/${auditId}/modules/${moduleId}/lock`)
  if (r.data.locked) {
    locked.value = true
    lockToken.value = r.data.lockToken
    lockInfo.value = '已锁定（15分钟心跳续期）'
    heartbeatTimer = setInterval(async () => {
      try {
        await http.post(`/api/audits/${auditId}/modules/${moduleId}/heartbeat`, { lockToken: lockToken.value })
      } catch { /* ignore */ }
    }, 60000)
  } else {
    locked.value = false
    lockInfo.value = '被他人占用：' + r.data.lockedByUserId
  }
}

async function loadClauses() {
  const r = await http.get(`/api/audits/${auditId}/modules/${moduleId}`)
  clauses.value = r.data.items.map(x => ({ ...x, _saving: false, _saved: false }))
}

async function saveClause(c) {
  c._saved = false
  c._saving = true
  try {
    await http.put(`/api/audits/${auditId}/clauses/${c.clauseId}`, { status: c.status, comment: c.comment })
    c._saved = true
    setTimeout(() => (c._saved = false), 1500)
  } catch (e) {
    err.value = e?.response?.data?.message || '保存失败'
  } finally {
    c._saving = false
  }
}

async function uploadPhotos(c, e) {
  err.value = ''
  const files = Array.from(e.target.files || [])
  if (!files.length) return
  if ((c.photos?.length || 0) + files.length > 3) {
    err.value = '每条款最多3张照片'
    return
  }
  const fd = new FormData()
  for (const f of files) {
    if (f.size > 10 * 1024 * 1024) {
      err.value = '单张照片不能超过10MB'
      return
    }
    fd.append('files', f)
  }
  try {
    const r = await http.post(`/api/audits/${auditId}/clauses/${c.clauseId}/photos`, fd, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    // reload to get latest list
    await loadClauses()
  } catch (ex) {
    err.value = ex?.response?.data?.message || '上传失败'
  } finally {
    e.target.value = ''
  }
}

async function reload() {
  err.value = ''
  await loadClauses()
}

async function cleanup() {
  if (heartbeatTimer) clearInterval(heartbeatTimer)
  heartbeatTimer = null
  if (lockToken.value) {
    try {
      await http.post(`/api/audits/${auditId}/modules/${moduleId}/unlock`, { lockToken: lockToken.value })
    } catch { /* ignore */ }
  }
}

onMounted(async () => {
  try {
    await tryLock()
    await loadClauses()
  } catch (e) {
    err.value = e?.response?.data?.message || '加载失败'
  }
})

onBeforeUnmount(cleanup)
</script>
