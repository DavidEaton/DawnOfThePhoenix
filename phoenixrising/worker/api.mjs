const json=(value,status=200)=>Response.json(value,{status,headers:{'Cache-Control':'no-store','X-Content-Type-Options':'nosniff'}});
const slug=v=>typeof v==='string'&&/^[a-zA-Z0-9_-]{1,100}$/.test(v)?v:'';
export function publicConfig(env){const user=slug(env.CALCOM_USERNAME);const thirty=slug(env.CALCOM_30_EVENT);const hour=slug(env.CALCOM_60_EVENT);return {bookingUrl:user?'https://cal.com/'+user:'',thirtyMinuteUrl:user&&thirty?'https://cal.com/'+user+'/'+thirty:'',sixtyMinuteUrl:user&&hour?'https://cal.com/'+user+'/'+hour:'',paymentsReady:!!env.STRIPE_SECRET_KEY,email:'dawn@dawnofthephoenix.com'};}
export async function checkout(request,env){
 if(request.method!=='POST')return json({error:'Method not allowed'},405);
 const origin=new URL(request.url).origin;
 if(request.headers.get('Origin')!==origin)return json({error:'Please start checkout from this website.'},403);
 if(!env.STRIPE_SECRET_KEY)return json({error:'Online payments are being set up. Please contact Dawn.'},503);
 let input;try{if(Number(request.headers.get('Content-Length')||0)>2048)return json({error:'Request too large'},413);input=await request.json()}catch{return json({error:'Invalid request'},400)}
 if(!['questions','tip'].includes(input.kind))return json({error:'Unknown service'},400);
 const amount=input.kind==='questions'?800:input.amount;
 if(!Number.isInteger(amount)||amount<100||amount>100000)return json({error:'Please choose a tip from $1 to $1,000.'},400);
 const params=new URLSearchParams({mode:'payment',success_url:origin+'/?checkout=complete&session_id={CHECKOUT_SESSION_ID}',cancel_url:origin+'/?checkout=cancelled','payment_method_types[0]':'card','line_items[0][quantity]':'1','line_items[0][price_data][currency]':'usd','line_items[0][price_data][unit_amount]':String(amount),'line_items[0][price_data][product_data][name]':input.kind==='questions'?'PhoenixRising · Three yes/no questions':'A little love for Dawn · Tip','metadata[service]':input.kind,'payment_intent_data[metadata][service]':input.kind,'consent_collection[terms_of_service]':'required','custom_text[terms_of_service_acceptance][message]':'Readings are for entertainment purposes only. No outcome is guaranteed. Terms: '+origin+'/terms'});
 if(input.kind==='questions'){
  params.set('line_items[0][price_data][product_data][description]','Three yes-or-no questions, answered by Dawn within 48 hours of payment. Enter your questions and reply email at checkout.');
  for(let n=0;n<3;n++){params.set(`custom_fields[${n}][key]`,'question'+(n+1));params.set(`custom_fields[${n}][label][type]`,'custom');params.set(`custom_fields[${n}][label][custom]`,'Yes/no question '+(n+1));params.set(`custom_fields[${n}][type]`,'text');params.set(`custom_fields[${n}][text][maximum_length]`,'150');}
 }
 try{const response=await fetch('https://api.stripe.com/v1/checkout/sessions',{method:'POST',headers:{Authorization:'Bearer '+env.STRIPE_SECRET_KEY,'Content-Type':'application/x-www-form-urlencoded','Stripe-Version':'2025-02-24.acacia'},body:params,signal:AbortSignal.timeout(15000)});const data=await response.json();if(!response.ok||!data.url)return json({error:'Checkout is temporarily unavailable. Please contact Dawn to arrange payment.'},502);const url=new URL(data.url);if(url.protocol!=='https:'||url.hostname!=='checkout.stripe.com')return json({error:'Checkout could not be opened.'},502);return json({url:data.url});}catch{return json({error:'Checkout is temporarily unavailable. Please try again later.'},502)}
}
export async function checkoutStatus(request,env){
 if(!env.STRIPE_SECRET_KEY)return json({error:'Payments are not configured.'},503);
 const id=new URL(request.url).searchParams.get('session_id');if(!id||!/^cs_(test_|live_)?[A-Za-z0-9]{10,250}$/.test(id))return json({error:'Invalid checkout session'},400);
 try{const r=await fetch('https://api.stripe.com/v1/checkout/sessions/'+encodeURIComponent(id),{headers:{Authorization:'Bearer '+env.STRIPE_SECRET_KEY},signal:AbortSignal.timeout(10000)});if(!r.ok)return json({error:'Unable to verify payment'},502);const s=await r.json();return json({paid:s.payment_status==='paid',kind:s.metadata?.service||null});}catch{return json({error:'Unable to verify payment'},502)}
}
