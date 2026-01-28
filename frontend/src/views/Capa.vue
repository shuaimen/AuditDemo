<template>
  <div class="card">
    <div style="display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap">
      <div>
        <router-link :to="'/audits/' + auditId">← 返回评鉴</router-link>
        <div style="font-size:20px;font-weight:700;margin-top:6px">整改项（CAPA）</div>
      </div>
      <div>
        <button class="secondary" @click="load">刷新</button>
      </div>
    </div>

    <div v-if="err" class="bad" style="margin-top:10px">{{ err }}</div>
  </div>

  <div class="card" v-if="loading">加载中...</div>

  <div class="card" v-else>
    <div v-if="items.length === 0" style="color:#666">暂无整改项（请先在评鉴详情页执行“判级并生成整改项”）</div>

    <div v-for="c in items" :key="c.capaId" class="card" style="background:#fbfbfe">
      <div class="row">
        <div class="col" style="min-width:220px">
          <div><b>条款：</b>{{ c.clauseCode }}</div>
          <div style="margin-top:6px">
            <span class="badge">{{ c.statusText }}</span>
            <span class="badge" style="margin-left:8px">截止：{{ c.dueDate ? formatDate(c.dueDate) : '未填写' }}</span>
          </div>
        </div>
        <div class="col" style="display:flex;align-items:center;justify-content:end;gap:10px">
          <button class="secondary" @click="save(c)">保存</button>
          <button class="secondary" v-if="c.status===1" @click="submitEvidence(c)">提交证据</button>
          <button class="secondary" v-if="c.status===2" @click="close(c)">关闭</button>
        </div>
      </div>

      <div class="row" style="margin-top:10px">
        <div class="col">
          <label>整改措施</label>
          <textarea v-model="c.correctiveAction" placeholder="填写整改措施"></textarea>
        </div>
        <div class="col">
          <label>责任人姓名（外部）</label>
          <input v-model="c.externalOwnerName" placeholder="工厂联系人" />
        </div>
        <div class="col">
          <label>责任人电话（外部）</label>
          <input v-model="c.externalOwnerPhone" placeholder="电话" />
        </div>
        <div class="col">
          <label>截止日期</label>
          <input type="date" v-model="c._dueDate" />
        </div>
      </div>

      <div class="row" style="margin-top:10px">
        <div class="col">
          <label>复核结论（关闭前可填写）</label>
          <textarea v-model="c.reviewConclusion" placeholder="复核结论"></textarea>
        </div>
        <div class="col">
          <label>证据照片（最多5张，单张≤10MB）</label>
          <input type="file" accept="image/*" capture="environment" multiple @change="(e)=>uploadEvidence(c,e)" />
          <div style="display:flex;gap:8px;flex-wrap:wrap;margin-top:8px">
            <a v-for="p in c.evidence" :key="p.evidenceId" :href="p.url" target="_blank">
              <img :src="p.url" style="width:86px;height:86px;object-fit:cover;border-radius:8px;border:1px solid #ddd" />
            </a>
          </div>
        </div>
      </div>

      <div v-if="c._msg" style="margin-top:8px;color:#666">{{ c._msg }}</div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { http } from '../api/http'

const route = useRoute()
const auditId = route.params.auditId

const items = ref([])
const loading = ref(false)
const err = ref('')

function formatDate(d) {
  const dt = new Date(d)
  const y = dt.getFullYear()
  const m = String(dt.getMonth() + 1).padStart(2, '0')
  const day = String(dt.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

function toDateInput(d) {
  if (!d) return ''
  const dt = new Date(d)
  const y = dt.getFullYear()
  const m = String(dt.getMonth() + 1).padStart(2, '0')
  const day = String(dt.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

async function load() {
  err.value = ''
  loading.value = true
  try {
    const r = await http.get(`/api/capa/by-audit/${auditId}`)
    items.value = r.data.items.map(x => ({
      ...x,
      _dueDate: toDateInput(x.dueDate),
      _msg: ''
    }))
  } catch (e) {
    err.value = e?.response?.data?.message || '加载失败'
  } finally {
    loading.value = false
  }
}

async function save(c) {
  c._msg = ''
  try {
    const body = {
      correctiveAction: c.correctiveAction || null,
      externalOwnerName: c.externalOwnerName || null,
      externalOwnerPhone: c.externalOwnerPhone || null,
      dueDate: c._dueDate ? c._dueDate : null,
      reviewConclusion: c.reviewConclusion || null
    }
    await http.put(`/api/capa/${c.capaId}`, body)
    c._msg = '已保存'
    setTimeout(() => (c._msg = ''), 1200)
    await load()
  } catch (e) {
    err.value = e?.response?.data?.message || '保存失败'
  }
}

async function uploadEvidence(c, e) {
  err.value = ''
  const files = Array.from(e.target.files || [])
  if (!files.length) return
  if ((c.evidence?.length || 0) + files.length > 5) {
    err.value = '每个整改项最多5张证据照片'
    e.target.value = ''
    return
  }

  const fd = new FormData()
  for (const f of files) {
    if (f.size > 10 * 1024 * 1024) {
      err.value = '单张照片不能超过10MB'
      e.target.value = ''
      return
    }
    fd.append('files', f)
  }

  try {
    await http.post(`/api/capa/${c.capaId}/evidence`, fd, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    await load()
  } catch (ex) {
    err.value = ex?.response?.data?.message || '上传失败'
  } finally {
    e.target.value = ''
  }
}

async function submitEvidence(c) {
  err.value = ''
  try {
    // 会触发 DB CHECK：要求责任人/电话/截止日期已填写
    await http.post(`/api/capa/${c.capaId}/submit-evidence`)
    await load()
  } catch (e) {
    err.value = e?.response?.data?.message || '提交失败（请确认已填写责任人/电话/截止日期）'
  }
}

async function close(c) {
  err.value = ''
  if (!c.reviewConclusion || !c.reviewConclusion.trim()) {
    err.value = '请填写复核结论后再关闭'
    return
  }
  try {
    await http.post(`/api/capa/${c.capaId}/close`, { reviewConclusion: c.reviewConclusion })
    await load()
  } catch (e) {
    err.value = e?.response?.data?.message || '关闭失败'
  }
}

onMounted(load)
</script>
